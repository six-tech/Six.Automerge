# Requirements: CollaborativeEditor Crash & Sync Stability

## Problem Statement

The `DemoApp.CollaborativeEditor` frequently crashes or stops syncing when one client types heavily (with returns, deletes, rapid edits) while other clients are syncing. Symptoms include hard crashes (window closes), UI freezes (deadlocks), and silent sync failure (app runs but stops receiving updates). Unit tests cannot reproduce this because the bugs are in the integration between FFI, sync, UI thread, and network layers.

## Root Cause Analysis (from code review)

Six distinct crash vectors identified across three layers:

### Layer 1: Sync Infrastructure (`DocSynchronizer`, `CollectionSynchronizer`, `Repo`)
1. **Nullable document lock in DocSynchronizer** — `_getDocumentLock()` can return null, causing FFI calls (`ReceiveSyncMessage`, `GenerateSyncMessage`, `Save`) to execute without serialization. Concurrent FFI access on a non-thread-safe Rust library causes segfaults.
2. **`_updateDocument()` called outside lock** — After receiving a sync message and releasing the lock, `_updateDocument(doc)` is invoked. Another thread can mutate the document between lock release and callback.
3. **Multiple lock acquisitions in `SendSyncMessage`** — Lock is fetched, held, released, then re-fetched for a second operation. Document state can change between the two critical sections.
4. **Blocking `Wait()` in `DocHandle.NotifyRemoteChange()`** — Called from sync callback path. Synchronously blocks the thread waiting for `_documentLock`. Under heavy sync traffic, this deadlocks the thread pool.
5. **Fire-and-forget tasks in Repo** — `UpdateDocument`, `RequestDocument`, `OnSynchronizerSyncStateChanged`, and `Delete` all use `Task.Run` without observing results. Exceptions are silently lost.
6. **`_handleLock` double-release in `Repo.Find()`** — Lock released before awaiting in-progress find, then conditionally released again, risking `SemaphoreFullException`.

### Layer 2: FFI Bindings (`AutomergeDocument`, `AutomergeSyncState`)
7. **23+ public methods missing `ThrowIfDisposed()` checks** — Including `SpliceText`, all `Put*` methods, `ReceiveSyncMessage`, `GenerateSyncMessage`. If a document is disposed during sync (e.g., on disconnect), these crash with segfault/NullRef from the native layer.
8. **`AutomergeSyncState` has no `_disposed` flag** — Use-after-dispose goes directly to FFI.

### Layer 3: UI ViewModel (`EditorViewModel`)
9. **`_isRemoteUpdate` flag is not thread-safe** — Set/unset in `RefreshFromDocument()` without synchronization. If two remote changes arrive rapidly, the flag can be corrupted, causing the `DocumentText` setter to call `ApplyTextChange()` recursively during a remote update.
10. **Silent exception swallowing** — Cursor restoration, save operations, and various callbacks catch all exceptions silently, hiding the real root causes.

## Functional Requirements

### FR-1: Diagnostic Logging
- Add structured logging (`ILogger`) to all sync operations in `DocSynchronizer`, `CollectionSynchronizer`, `DocHandle`, and `Repo`
- Log lock acquisitions/releases, FFI calls, sync message exchange, and document state transitions
- All logs must include `DocumentId` and `PeerId` for correlation in Aspire traces
- Log exceptions that are currently silently swallowed

### FR-2: Thread Safety Fixes
- Ensure document lock is always non-null when FFI calls are made in `DocSynchronizer`
- Hold lock for the full duration of `ReceiveSyncMessage` + `_updateDocument` callback
- Hold lock for full `SendSyncMessage` operation (generate + optional save)
- Make `DocHandle.NotifyRemoteChange()` async or use non-blocking lock pattern
- Fix `_handleLock` double-release in `Repo.Find()`

### FR-3: Disposal Safety
- Add `ThrowIfDisposed()` to all public mutation methods in `AutomergeDocument`
- Add `_disposed` flag and checks to `AutomergeSyncState`
- Ensure sync operations gracefully handle disposed documents (catch `ObjectDisposedException`, log, skip)

### FR-4: UI Thread Safety
- Make `_isRemoteUpdate` flag atomic or use a proper synchronization mechanism
- Ensure `RefreshFromDocument()` is safe against concurrent remote changes

### FR-5: Error Visibility
- Replace silent `catch {}` blocks with logged warnings/errors
- Ensure fire-and-forget tasks log failures to `ILogger`
- Surface errors in Aspire traces/logs for debugging

### FR-6: E2E Verification
- Start full stack via `Infra.AppHost` (Aspire)
- Create a document in one client, join from other clients
- Type heavily in one client (text, returns, deletes, rapid edits) via `six-avalonia-mcp`
- Verify all clients stay connected and reflect changes
- Observe Aspire traces/logs throughout for errors

## Non-Functional Requirements

### NFR-1: No Behavioral Regressions
- All existing 1,028 unit tests must continue passing
- Sync protocol behavior must remain unchanged (only safety wrappers added)

### NFR-2: Performance
- Lock contention changes must not degrade typing latency noticeably
- Logging must use structured logging with minimal allocation (no string interpolation in hot paths)

### NFR-3: Observability
- All new logs must be compatible with OpenTelemetry (Aspire traces)
- Use `LogLevel.Debug` for high-frequency operations, `LogLevel.Warning`/`Error` for failures

### NFR-4: Minimal Code Changes
- Fix only identified crash vectors — no speculative refactoring
- Keep existing API surface unchanged
