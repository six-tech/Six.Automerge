# 2026-02-22 15:27:11 UTC | Spec: document-sync-issues-now-when-i-run-aspire-ho | Task: sprint-1-task-4 | Sprint: Fix FileSystemStorageAdapter Empty Prefix (Storage Layer)

**Description**: Run existing FileSystem storage tests (`dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit`) and ensure all 20 existing tests still pass plus the new tests.

FileSystemStorageAdapter empty prefix fix: Both `GetChunkInfo()` and `LoadRange()` now use `_baseDirectory` when `keyPrefix.Components.Count == 0` instead of calling `GetFilePath()` which throws. `RemoveRange` was intentionally left unchanged (destructive). Test count: 22 pass (was 20).

# 2026-02-22 15:43:54 UTC | Spec: document-sync-issues-now-when-i-run-aspire-ho | Task: sprint-3-task-9 | Sprint: Fix TextDocument Operation Serialization (Critical Crash Fix)

**Description**: Run existing TextEdit tests (`dotnet test tests/Six.Automerge.TextEdit.Tests.Unit`) — all 102 existing tests must pass. Fix any tests that depend on the old debouncer or `_isLocalChange` flag behavior.

TextDocument channel-based operation queue: When replacing _isLocalChange with a Channel<TextOperation> queue, local operations (text changes, marks) still trigger the DocHandle.Changed event synchronously. Use a volatile _processingLocal flag set/cleared around ChangeAsync calls within the processing loop to suppress spurious RemoteRefresh writes. This is safe because Changed fires synchronously on the same thread as ChangeAsync. Mark operations route through the queue with TaskCompletionSource for awaitability.

# 2026-02-22 15:51:46 UTC | Spec: document-sync-issues-now-when-i-run-aspire-ho | Task: sprint-4-task-5 | Sprint: Update Tests for TextDocument Queue + Fix ChangeDebouncer

**Description**: Run all DemoApp.Shared tests (`dotnet test tests/DemoApp.Shared.Tests`) — all 86 tests must pass.

Sprint 4 test counts: TextEdit.Tests.Unit now 106 (was 102, added 4 queue tests). DemoApp.Shared.Tests now 93 (was 86). CollaborativeEditor.Tests still 82. ChangeDebouncer.cs is unused by TextDocument (replaced by channel queue in Sprint 3) but kept in codebase alongside its 8 tests. TextDocumentOptions.ChangeDebounceMs property is also no longer consumed by TextDocument.

# 2026-02-22 16:16:27 UTC | Spec: document-sync-issues-now-when-i-run-aspire-ho | Task: sprint-5-task-7 | Sprint: Document Discovery Notification (SignalR Layer)

**Description**: Run existing SignalR adapter tests and Server.Api tests to ensure no regressions. Add a unit test for `SyncServer.DocumentAvailable` event firing.

## Sprint 5 - Document Discovery Notification (SignalR Layer) - Completed

### Implementation Summary
The document discovery notification chain:
1. **SyncServer.cs**: Added `DocumentAvailable` event + `DocumentAvailableEventArgs`. Fires in `OnMessageReceivedAsync` when `_knownDocumentIds.TryAdd()` returns true (first time seeing a document ID).
2. **IDocumentsHubClient**: Added `Task OnDocumentAvailable(string documentId)` to the interface.
3. **SyncServerHostedService**: Modified to take `IHubContext<DocumentsHub, IDocumentsHubClient>`. In `StartAsync`, subscribes to `SyncServer.DocumentAvailable`. When fired, calls `hubContext.Clients.All.OnDocumentAvailable(documentId)`. Unsubscribes in `StopAsync`.
4. **SignalRNetworkAdapter**: Added `DocumentAvailable` event + `DocumentAvailableEventArgs`. Implemented `IDocumentsHubClient.OnDocumentAvailable`. Registered callback `_connection.On<string>("OnDocumentAvailable", ...)` in `ConnectAsyncInternal`.
5. **RepoService**: Added `DocumentsChanged` event. Subscribes to `_networkAdapter.DocumentAvailable`. Maintains `_knownDocumentIds` HashSet. Fires `DocumentsChanged` when a new document ID is added.
6. **DocumentSessionDialogViewModel**: Added `SubscribeToDocumentsChanged(RepoService)` and `UnsubscribeFromDocumentsChanged()`. When `DocumentsChanged` fires, calls `LoadServerDocumentsAsync`.
7. All 3 demo apps (Editor, Drawing, TaskBoard) wired to call `_sessionVm.SubscribeToDocumentsChanged(repoService)`.

### Test Counts After Sprint 5
- SyncServer.Tests.Unit: 28 (was 23, +5 new including 2 DocumentAvailable tests)
- Server.Api.Tests.Unit: 72 (was 41, +31 from previous sprints, +1 new StartAsync broadcast test)
- DemoApp.Shared.Tests: 97 (was 93, +4 new)
- CollaborativeEditor.Tests: 82 (unchanged)
- TextEdit.Tests.Unit: 106 (unchanged)
- Repo.Tests.Unit: 266 (unchanged)

### Key Pattern: Broadcasting from Outside a Hub
When you need to broadcast to SignalR clients from outside a hub (e.g., from an event handler), use `IHubContext<THubType, TClientInterface>` injected via DI, not from within the hub class itself (hubs are transient). The `SyncServerHostedService` pattern is a good example.

### Fix: Server.Api.Tests.Unit Missing Native Libs
Added `CopyNativeLibs` MSBuild target to Server.Api.Tests.Unit.csproj — was missing, causing `DllNotFoundException` when tests used `AutomergeDocument.Create()`.

# 2026-02-22 16:55:20 UTC | Spec: document-sync-issues-now-when-i-run-aspire-ho | Task: sprint-6-task-8 | Sprint: Integration Testing & End-to-End Validation

**Description**: Type on both clients simultaneously. Verify collaborative editing works without crashes or text corruption.

Sprint 6 E2E validation results (2026-02-22): Full Aspire stack works correctly. 1,157 tests passing. Document creation, discovery, unidirectional sync, and bidirectional concurrent editing all verified with live MCP inspection. Zero StorageKey errors. SignalR integration tests (4 failures) require running Aspire test server with Docker - pre-existing infrastructure issue, not related to code changes.

