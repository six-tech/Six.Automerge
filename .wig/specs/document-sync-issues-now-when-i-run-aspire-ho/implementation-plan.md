# Implementation Plan: Document Sync Issues Fix

## Sprint 1: Fix FileSystemStorageAdapter Empty Prefix (Storage Layer)

This sprint fixes the lowest layer — the storage adapter that causes `ListDocuments` to fail on the server.

- [ ] In `FileSystemStorageAdapter.GetChunkInfo()` (line 286-325), add an early check: if `keyPrefix.Components.Count == 0`, scan all subdirectories of `_baseDirectory` instead of calling `GetFilePath(keyPrefix)`. Enumerate all 2-char shard directories, walk each recursively, parse file paths back to `StorageKey` via `ParseFilePathToKey`, and collect `ChunkInfo` results. Keep the existing logic for non-empty prefixes unchanged.
- [ ] Similarly update the base `LoadRange()` behavior — check if `FileSystemStorageAdapter` overrides `LoadRange`. If `LoadRange` also goes through `GetFilePath`, add the same empty-prefix handling. If it uses the `StorageAdapterBase.LoadRange()` which calls `GetChunkInfo()` → `Load()`, then `Load()` with a real key should be fine once `GetChunkInfo` returns correct results.
- [ ] Add unit tests in `tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit/`: (a) `GetChunkInfo_EmptyPrefix_ReturnsAllStoredChunks` — store 3 documents, call `GetChunkInfo(empty)`, assert all chunk keys returned. (b) `LoadRange_EmptyPrefix_ReturnsAllStoredData` — same setup, verify data is returned.
- [ ] Run existing FileSystem storage tests (`dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit`) and ensure all 20 existing tests still pass plus the new tests.

## Sprint 2: Fix SyncServer Document Tracking & Listing

This sprint makes `ListDocumentsAsync()` work correctly by combining in-memory tracking with storage scanning.

- [ ] In `SyncServer.cs`, add a `ConcurrentDictionary<string, byte> _knownDocumentIds` field. In `OnMessageReceivedAsync()` (line 104-119), after the null-check on `message.DocumentId`, add `_knownDocumentIds.TryAdd(documentId.Value, 0)`. In `OnDocumentLoaded()` (line 172-175), add `_knownDocumentIds.TryAdd(e.DocumentId.Value, 0)`.
- [ ] In `ListDocumentsAsync()` (line 127-145), change the implementation to: (1) start with `new HashSet<string>(_knownDocumentIds.Keys)`, (2) also scan storage via `GetChunkInfo(emptyPrefix)` to catch documents from previous server runs, (3) union the two sets. Wrap the storage scan in a try/catch so that if storage is empty/unavailable, the in-memory set is still returned.
- [ ] In `DeleteDocumentAsync()` (line 162-170), add `_knownDocumentIds.TryRemove(documentId, out _)` before the storage cleanup.
- [ ] Add unit tests in `tests/Six.Automerge.SyncServer.Tests.Unit/`: (a) `ListDocumentsAsync_AfterReceivingMessage_ReturnsDocumentId` — send a sync message for a document, verify `ListDocumentsAsync` returns it immediately without waiting for persistence. (b) `ListDocumentsAsync_CombinesInMemoryAndStorage` — pre-populate storage with one doc, send message for another, verify both returned.
- [ ] Run existing SyncServer tests (`dotnet test tests/Six.Automerge.SyncServer.Tests.Unit`) — all 23 existing tests must pass plus new tests.

## Sprint 3: Fix TextDocument Operation Serialization (Critical Crash Fix)

This sprint replaces the race-prone `_isLocalChange` flag with a serialized `Channel<T>` operation queue in `TextDocument`, eliminating the Rust panic.

- [ ] In `TextDocument.cs`, add a nested abstract record `TextOperation` with two variants: `LocalTextChange(string NewText)` and `RemoteRefresh`. Add fields: `Channel<TextOperation> _operationQueue` (unbounded), `CancellationTokenSource _queueCts`, `Task? _processingTask`. Remove the `_isLocalChange` field and `ChangeDebouncer? _debouncer` field.
- [ ] Implement `ProcessOperationQueueAsync()` as an async method that reads from the channel in a loop (`await foreach`). For `LocalTextChange`: drain/coalesce additional pending `LocalTextChange` items from the queue, then call a new `FlushLocalChangeAsync(string)` method. For `RemoteRefresh`: call `RefreshFromDocumentAsync` and fire `TextChanged`/`FormattedTextChanged` events with `isLocal=false`. Handle `ObjectDisposedException` and `OperationCanceledException` for graceful shutdown.
- [ ] Refactor `FlushLocalChangeAsync(string newText)` from the existing `FlushTextChangeAsync`: compute splice against `_cachedText`, apply via `_handle.ChangeAsync(doc => doc.SpliceText(...))`, then call `RefreshFromDocumentAsync` and fire `TextChanged` with `isLocal=true`. No need for `_isLocalChange` flag since queue serialization guarantees exclusion.
- [ ] Update `ApplyTextChangeAsync()` to simply write `new LocalTextChange(newText)` to the channel writer. Remove the debouncer creation logic.
- [ ] Update `OnDocHandleChanged()` to write `new RemoteRefresh()` to the channel writer instead of fire-and-forget `HandleRemoteChangeAsync()`. Remove the `_isLocalChange` check.
- [ ] Start the processing task in `InitializeAsync()` after subscribing to `Changed`: `_processingTask = Task.Run(() => ProcessOperationQueueAsync())`.
- [ ] Update `Dispose()`: cancel `_queueCts`, complete the channel writer, await `_processingTask` (with timeout). Remove debouncer disposal.
- [ ] Update `ApplyMarkAsync` and `RemoveMarkAsync` methods: these currently set `_isLocalChange = true/false` directly. Instead, they should flush any pending queue items first (complete channel, await, restart), apply the mark change, refresh, then restart the queue. Alternative: send a special `MarkOperation` through the queue. Choose the simpler approach: since marks are infrequent, flush the queue, apply mark directly, then restart.
- [ ] Run existing TextEdit tests (`dotnet test tests/Six.Automerge.TextEdit.Tests.Unit`) — all 102 existing tests must pass. Fix any tests that depend on the old debouncer or `_isLocalChange` flag behavior.

## Sprint 4: Update Tests for TextDocument Queue + Fix ChangeDebouncer

This sprint updates/adds tests for the new queue-based TextDocument and cleans up the debouncer.

- [ ] Add new unit tests in `tests/Six.Automerge.TextEdit.Tests.Unit/` for the queue behavior: (a) `ApplyTextChangeAsync_RapidChanges_CoalescesCorrectly` — schedule 10 rapid changes, verify only the final text is committed. (b) `RemoteRefresh_DuringPendingLocalChange_IsDeferred` — enqueue a local change then a remote refresh, verify local processes first. (c) `ConcurrentLocalAndRemote_DoesNotCrash` — simulate rapid alternating local/remote operations. (d) `Dispose_FlushesQueueGracefully` — verify pending changes are flushed on dispose.
- [ ] Review `ChangeDebouncer.cs` usage: if it's only used by `TextDocument`, and `TextDocument` no longer uses it, check if any other code references it. If unused, leave it in place (don't delete unless confirmed unused) but remove the internal reference from `TextDocument`.
- [ ] Remove the `Console.Error.WriteLine` debug output from `EditorViewModel.FormattedText` setter (line 87 of `EditorViewModel.cs`).
- [ ] Run all CollaborativeEditor tests (`dotnet test tests/DemoApp.CollaborativeEditor.Tests`) — all 82 tests must pass. Fix any that rely on the old `TextDocument` internals.
- [ ] Run all DemoApp.Shared tests (`dotnet test tests/DemoApp.Shared.Tests`) — all 86 tests must pass.

## Sprint 5: Document Discovery Notification (SignalR Layer)

This sprint adds the server-to-client notification when new documents become available.

- [ ] In `SyncServer.cs`, add a public event: `event EventHandler<DocumentAvailableEventArgs>? DocumentAvailable` with `DocumentAvailableEventArgs { string DocumentId }`. Fire this event in `OnMessageReceivedAsync` when `_knownDocumentIds.TryAdd` returns true (first time seeing this document ID).
- [ ] In `IDocumentsHubClient` interface (in `Six.Automerge.Repo.Network.SignalR.Abstractions`), add: `Task OnDocumentAvailable(string documentId)`.
- [ ] In `DocumentsHub.cs` (or the service startup), subscribe to `SyncServer.DocumentAvailable`. When fired, call `Clients.All.OnDocumentAvailable(documentId)` to notify all connected clients. Consider using `Clients.Others` if the originating client shouldn't be notified (but it's harmless to notify them too).
- [ ] In `SignalRNetworkAdapter.cs`, register a callback for `OnDocumentAvailable` in `ConnectAsyncInternal()`. Add a public event `DocumentAvailable` and raise it when the callback fires. Implement `IDocumentsHubClient.OnDocumentAvailable`.
- [ ] In `RepoService.cs`, subscribe to the adapter's `DocumentAvailable` event. Maintain a list of known document IDs and add the new one. Fire a new event `DocumentsChanged` that the UI can subscribe to.
- [ ] In `DocumentSessionDialogViewModel.cs`, subscribe to `RepoService.DocumentsChanged`. When fired, auto-refresh the available documents list (call `LoadServerDocumentsAsync`).
- [ ] Run existing SignalR adapter tests and Server.Api tests to ensure no regressions. Add a unit test for `SyncServer.DocumentAvailable` event firing.

## Sprint 6: Integration Testing & End-to-End Validation

This sprint validates the complete fix with the Aspire stack and running clients.

- [ ] Build the full solution (`dotnet build`) and verify zero errors/warnings.
- [ ] Run ALL test projects (`dotnet test` from repo root or use `tests-run.cs` script) — target: all 1,083+ tests passing (including new tests from previous sprints).
- [ ] Start Aspire AppHost (`dotnet run --project apps/Infra.AppHost`) and verify the dashboard shows all resources healthy.
- [ ] Use `six-avalonia-mcp` to launch a CollaborativeEditor client (replica-1). Create a new document. Verify no crash, text displays correctly.
- [ ] Verify the server console shows NO `StorageKey must have at least one component` errors.
- [ ] From a second client (replica-2), verify the document session dialog shows the document created by replica-1 (either via auto-refresh notification or manual refresh).
- [ ] Open the document on replica-2. Type several sentences on replica-1. Verify: (a) no crash, (b) no garbled text on replica-1, (c) text appears correctly on replica-2.
- [ ] Type on both clients simultaneously. Verify collaborative editing works without crashes or text corruption.
