# Requirements: Document Sync Issues Fix

## Context

After recent refactorings, the Aspire-hosted collaborative editing stack has three critical bugs that prevent normal multi-peer document editing:

1. **Server document listing always fails** (StorageKey empty prefix error)
2. **Documents created on one client are invisible to others** (no discovery/announcement)
3. **Client crashes after typing several characters** (Rust panic from conflicting text CRDT operations)

## Functional Requirements

### FR-1: FileSystemStorageAdapter Must Handle Empty Prefix Queries

- `FileSystemStorageAdapter.GetChunkInfo(StorageKey)` MUST accept an empty `StorageKey` (zero components) and return metadata for ALL stored chunks
- `FileSystemStorageAdapter.LoadRange(StorageKey)` MUST accept an empty `StorageKey` and return ALL stored chunks
- The behavior MUST match `InMemoryStorageAdapter` which already handles empty prefixes correctly
- `StorageKey.StartsWith(emptyPrefix)` already returns `true` for all keys; the adapter must not short-circuit with an exception before reaching this check

### FR-2: SyncServer Must Track Document IDs In-Memory

- `SyncServer` MUST maintain an in-memory `HashSet<string>` of known document IDs
- When a sync message is received for a document (`OnMessageReceivedAsync`), the document ID MUST be added to the in-memory set
- `ListDocumentsAsync()` MUST return the union of in-memory tracked IDs and storage-persisted IDs
- This ensures documents are discoverable immediately after first sync, not only after persistence flush

### FR-3: Document Discovery Must Work Across Clients

- When the server's SyncServer first learns about a new document (via a sync message from any client), it MUST notify all connected clients
- The notification MUST use the existing `IDocumentsHubClient.OnReceiveMessage` or a new hub callback method
- Clients receiving a "new document available" notification MUST be able to list/open/join the document
- The document session UI SHOULD auto-refresh or provide a notification when new documents become available

### FR-4: Text CRDT Operations Must Be Serialized

- All text change operations (local edits + remote sync refreshes) in `TextDocument` MUST be serialized through a single async processing queue
- Local text changes (from `ApplyTextChangeAsync`) and remote change processing (from `HandleRemoteChangeAsync`) MUST NOT run concurrently
- The splice computation (`TextDiff.ComputeSplice`) MUST always run against the latest committed `_cachedText`, never a stale version
- The `_isLocalChange` flag MUST remain `true` for the entire duration of local change processing, including the post-commit refresh

### FR-5: Echo-Back Sync Messages Must Not Corrupt Client State

- When the server echoes a client's own changes back (normal sync protocol behavior), the client MUST handle this gracefully
- The `TextDocument.OnDocHandleChanged` handler MUST NOT trigger a full text refresh for changes that originated locally
- The `_isLocalChange` flag timing MUST be fixed so that echo-back messages arriving after `ChangeAsync` completes but before `RefreshFromDocumentAsync` completes are correctly identified as local
- Even if an echo-back message creates a no-op `ReceiveSyncMessage`, the resulting `Changed` event MUST NOT interfere with in-progress local operations

### FR-6: Client Must Not Crash During Normal Typing

- The Rust panic (`assertion failed: self.data[index].is_none()` in `collector.rs:300`) MUST be eliminated
- The root cause is overlapping/duplicate text splice operations sent to the Automerge document; FR-4 and FR-5 together must prevent this
- The client MUST handle at least 100 WPM typing speed without garbled text or crashes
- Text content displayed in the editor MUST match the Automerge document state at all times (eventual consistency)

## Non-Functional Requirements

### NFR-1: Backward Compatibility

- All existing unit tests (1,083 total across all test projects) MUST continue to pass
- The `IStorageAdapter` interface MUST NOT change (the fix is in the FileSystem implementation)
- The `INetworkAdapter` interface MUST NOT change
- The `TextDocument` public API (`ApplyTextChangeAsync`, `ApplyMarkAsync`, `RemoveMarkAsync`) MUST NOT change signatures

### NFR-2: Performance

- The async queue for text operations MUST NOT add perceptible latency to keystrokes (< 5ms overhead per operation)
- `FileSystemStorageAdapter.GetChunkInfo` with empty prefix MUST NOT perform a full recursive directory scan on every call if avoidable (cache or optimize)
- Server in-memory document tracking MUST use thread-safe collections with O(1) lookup

### NFR-3: Thread Safety

- All fixes MUST maintain thread safety in concurrent scenarios (multiple peers editing simultaneously)
- The text operation queue MUST use `Channel<T>` or equivalent lock-free async pattern
- The `_isLocalChange` flag replacement MUST be concurrency-safe (consider `SemaphoreSlim` or `AsyncLocal<T>`)

### NFR-4: Diagnostics

- All error paths MUST log at appropriate levels (Error for failures, Debug for expected no-ops)
- Remove the `Console.Error.WriteLine` debug output from `EditorViewModel.FormattedText` setter (line 87)
- Server `ListDocuments` failures MUST log the actual error, not silently return empty list
