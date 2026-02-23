# Requirements: DemoApp.CollaborativeEditor Refactoring

## Overview

Major refactoring of the collaborative editor demo to properly use the Automerge Repo pattern, extract reusable libraries for text editing and server-side sync, fix crashes, enable rich text rendering, and eliminate duplicate projects.

## Functional Requirements

### FR-1: Proper Repo Pattern Usage

- **FR-1.1**: ALL document mutations MUST go through `DocHandle.ChangeAsync()`. No direct `AutomergeDocument` manipulation outside the DocHandle mutation callback.
- **FR-1.2**: ALL document reads MUST go through `DocHandle.ReadAsync()` or access the document only within a ChangeAsync/ReadAsync callback.
- **FR-1.3**: The `AutomergeService` class in DemoApp.CollaborativeEditor MUST be removed. Its functionality moves to `Six.Automerge.TextEdit`.
- **FR-1.4**: The EditorViewModel MUST NOT hold references to `AutomergeDocument` or call FFI methods directly.

### FR-2: Six.Automerge.TextEdit Library

- **FR-2.1**: New library `libs/Six.Automerge.TextEdit/` providing reusable text editing over Automerge CRDT.
- **FR-2.2**: `TextDocument` class wrapping `DocHandle<T>` that provides:
  - Text content access (read current text)
  - Text editing via diff/splice (insert, delete, replace)
  - Mark operations (bold, italic, underline, link, custom marks)
  - Cursor management (save/restore stable cursors across edits)
  - Rich text model: `FormattedSpan` segments with formatting metadata
  - Change event for remote updates with formatted span diffs
- **FR-2.3**: Built-in change debouncing for rapid typing — batch multiple keystrokes into fewer Automerge commits (configurable interval, default 50ms).
- **FR-2.4**: Thread-safe by design — all operations async, no synchronous lock waits.
- **FR-2.5**: Exposes configuration for sync debounce rate that maps to `RepoConfig.SyncDebounceRate`.
- **FR-2.6**: `FormattedText` model representing text with inline formatting spans, suitable for rendering in any UI framework.

### FR-3: Six.Automerge.SyncServer Library

- **FR-3.1**: New library `libs/Six.Automerge.SyncServer/` providing plug-and-play server-side Automerge sync.
- **FR-3.2**: `SyncServer` class that:
  - Holds the authoritative copy of all documents (server is TRUTH)
  - Participates in the Automerge sync protocol as a peer
  - Uses passive share policy (never announces, only responds to requests)
  - Persists documents with configurable debounced saves (default 5s timer, not after every change)
  - Provides document management: list, delete, export
- **FR-3.3**: Transport-agnostic design — accepts an `IServerTransport` interface for message routing. SignalR implementation provided separately.
- **FR-3.4**: Flexible architecture to support future Orleans integration without breaking changes.
- **FR-3.5**: Per-app-type isolation — separate Repo instances per application type (e.g., "collaborative-editor", "collaborative-drawing").

### FR-4: Rich Text Rendering

- **FR-4.1**: Marks (bold, italic, underline) MUST be visually rendered in the editor UI.
- **FR-4.2**: Marks MUST persist in the CRDT and propagate to all connected peers.
- **FR-4.3**: When a peer applies marks, all other peers MUST see the formatted text.
- **FR-4.4**: Use Avalonia `InlineCollection` with `Run` and `Span` elements for rendering formatted text. Build a custom `RichTextEditor` control that combines editable input with formatted display.
- **FR-4.5**: The formatting toolbar (Bold/Italic/Underline buttons) MUST apply marks to the selected text range and update the display immediately.

### FR-5: Fully Async Architecture

- **FR-5.1**: NO synchronous `.Wait()`, `.GetAwaiter().GetResult()`, or `.Result` calls anywhere in the editor or library code. All async operations MUST use `await`.
- **FR-5.2**: All `DocHandle` access MUST use `ChangeAsync()`, `ReadAsync()`, not `Change()` (which internally calls `.GetAwaiter().GetResult()`).
- **FR-5.3**: No thread pool starvation — all lock acquisitions use `WaitAsync()`.

### FR-6: Sync Throttling

- **FR-6.1**: Add sync message throttling to `DocSynchronizer` — batch sync messages per peer using a configurable delay (default 100ms, matching TS reference).
- **FR-6.2**: `RepoConfig` gains a `SyncDebounceRate` property (default 100ms).
- **FR-6.3**: The throttle uses the "deferred-final" pattern (like TS reference): delays first call by N ms, resets timer on each subsequent call, guarantees final state is always sent.

### FR-7: Editor Status & UX

- **FR-7.1**: `SyncStatus` MUST update through the full lifecycle: "Creating..." → "Ready" / "Joining..." → "Syncing..." → "Ready" / "Resuming..." → "Ready".
- **FR-7.2**: Status MUST reflect real-time sync state: "Ready", "Syncing (N peers)", "Disconnected", "Reconnecting".
- **FR-7.3**: Connection status bar MUST show accurate peer count and connection state at all times.

### FR-8: Cleanup

- **FR-8.1**: Delete `apps/DemoApp.CollaborativeTextEditor/` entirely.
- **FR-8.2**: Delete `tests/DemoApp.CollaborativeTextEditor.Tests/` entirely.
- **FR-8.3**: Remove any AppHost references to CollaborativeTextEditor.
- **FR-8.4**: Remove Orleans dependencies from AppHost and Server.Api (Orleans is out of scope for this project).

## Non-Functional Requirements

### NFR-1: Stability

- **NFR-1.1**: No crashes during multi-peer collaborative editing (4 concurrent replicas typing simultaneously for 5+ minutes).
- **NFR-1.2**: Client crash MUST NOT cascade to other clients or the server.
- **NFR-1.3**: Server crash recovery: server restarts and loads documents from storage, clients reconnect and resume.

### NFR-2: Performance

- **NFR-2.1**: Typing latency < 16ms (60fps) — UI thread MUST NOT be blocked by CRDT operations.
- **NFR-2.2**: Sync latency < 500ms — remote changes visible within 500ms of commit on originating peer.
- **NFR-2.3**: Network efficiency: sync messages batched at 100ms intervals, not per-keystroke.
- **NFR-2.4**: Server persistence debounced at 5s intervals (configurable), not per-change.

### NFR-3: Testability

- **NFR-3.1**: Six.Automerge.TextEdit MUST be testable without UI framework (pure logic).
- **NFR-3.2**: Six.Automerge.SyncServer MUST be testable without network transport (in-memory adapter).
- **NFR-3.3**: All tests pass consistently (no flaky timing-dependent tests).

### NFR-4: Architecture

- **NFR-4.1**: Clear separation: Libraries (`Six.Automerge.*`) know nothing about Avalonia. Demo apps reference libraries.
- **NFR-4.2**: Six.Automerge.SyncServer is transport-agnostic — no SignalR dependency in the core library.
- **NFR-4.3**: Six.Automerge.TextEdit is UI-framework-agnostic — provides data models that any renderer can consume.
