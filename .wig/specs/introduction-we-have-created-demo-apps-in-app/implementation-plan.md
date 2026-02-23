# Implementation Plan: Client-Server Collaborative Demo Apps

## Sprint 1: Fix Critical Bugs — FileSystemStorageAdapter Deadlock & Cache

Fix the `FileSystemStorageAdapter` deadlock in `LoadRange()` and the stale cache bug in `RemoveRange()`. These are blocking issues that cause UI freezes when loading documents.

- [ ] In `FileSystemStorageAdapter.cs`, extract an internal `LoadCore(StorageKey key)` method that reads from cache first, then disk, without acquiring `_lock`. This method should contain the current body of `Load()` minus the lock acquisition
- [ ] Refactor `Load(StorageKey key)` to acquire `_lock`, call `LoadCore()`, and release `_lock`
- [ ] Refactor `LoadRange(StorageKey keyPrefix)` to call `LoadCore()` instead of `Load()` when iterating files inside its existing lock scope, eliminating the deadlock
- [ ] Fix `RemoveRange(StorageKey keyPrefix)` to clear all matching cache entries immediately after deleting the directory tree, by iterating cache keys and evicting any that start with the removed prefix
- [ ] Run existing `Storage.FileSystem.Tests.Unit` and `Storage.FileSystem.Tests.Integration` tests to verify no regressions. Add a new test for cold-cache `LoadRange()` (the previously deadlocking scenario)
- [ ] Add a test for `RemoveRange()` verifying that cached data is cleared immediately (not stale until expiration)

## Sprint 2: Fix Critical Bugs — Async Lock Fixes in Repo and Synchronizers

Fix synchronous `.Wait()` calls on `SemaphoreSlim` throughout `Repo.cs`, `CollectionSynchronizer.cs`, `DocSynchronizer.cs`, and `DocHandle.cs` that block the UI thread.

- [ ] In `CollectionSynchronizer.cs`, replace all `_lock.Wait()` calls with `await _lock.WaitAsync()`. Convert `AddDocument`, `RemoveDocument`, `OnPeerConnected`, `OnPeerDisconnected`, `OnMessageReceived`, and `NotifyDocumentChanged` to async methods. Update the interface/callers accordingly
- [ ] In `DocSynchronizer.cs`, replace all `_lock.Wait()` calls with `await _lock.WaitAsync()`. Convert `BeginSync`, `EndSync`, `HasPeer`, `GetPeerStatus`, `ReceiveMessage`, and `NotifyLocalChange` to async methods. Update callers in `CollectionSynchronizer`
- [ ] In `Repo.cs`, replace `_handleLock.Wait()` with `await _handleLock.WaitAsync()` in `Create<T>()` (convert to async), `Delete()`, `Clone()`, `Handles` property getter, and all other methods using synchronous waits. Replace `_saveLock.Wait()` similarly
- [ ] In `DocHandle.cs`, fix `CompositeSemaphore` to use `await _first.WaitAsync()` and `await _second.WaitAsync()` instead of synchronous `.Wait()`. This requires converting `CompositeSemaphore` to an async-disposable pattern or restructuring callers
- [ ] Add `ConfigureAwait(false)` to all `await` calls in `Repo.cs` that don't need UI context (especially `ScheduleSave`/`PerformSave` continuations, and internal storage/network operations)
- [ ] Run `Repo.Tests.Unit` (259 tests) and `FFI.Tests.Unit` (145 tests) to verify no regressions from async refactoring

## Sprint 3: Fix Demo App UI Responsiveness

Fix the demo app event handlers and async patterns that cause UI freezes.

- [ ] In all four demo app windows (`MainWindow.axaml.cs` for TextEditor/Drawing/TaskBoard, `EditorWindow.axaml.cs` for Editor), ensure `OnSessionResultReady` properly wraps all async operations in try/catch with user-visible error feedback. Ensure UI-thread dispatching with `Dispatcher.UIThread.InvokeAsync()` for any UI updates after async operations
- [ ] In `RepoService.cs`, add proper cancellation support to `InitializeAsync()`. Add a `CancellationToken` parameter. Add a configurable connection timeout (default 10s). Ensure `WhenReady().WaitAsync()` uses the cancellation token so the UI can cancel if needed
- [ ] In all four demo apps, update `HandleCreateAsync`, `HandleJoinAsync`, and `HandleResumeAsync` to show a loading indicator while operations are in progress, and display meaningful error messages on failure instead of silently freezing
- [ ] Build and run `DemoApp.CollaborativeTextEditor` via Aspire. Use `six-avalonia-mcp` to verify: the app starts without freezing, "Create New" works, the document session dialog is responsive
- [ ] Test the "Resume" flow by creating a document, closing the app, reopening, and clicking on the stored document. Verify no deadlock (this was the primary reproduction scenario for the `LoadRange` deadlock)

## Sprint 4: Server-Side Repo Infrastructure — ServerRepoManager & SignalRServerNetworkAdapter

Build the server-side document management infrastructure.

- [ ] Create `apps/Server.Api/src/Services/ServerRepoOptions.cs` with `StorageBaseDirectory` property (default: `Path.Combine(AppContext.BaseDirectory, "documents")`)
- [ ] Create `apps/Server.Api/src/Services/ServerRepoManager.cs` as a singleton `IHostedService`. It should create and manage one `Repo` instance per registered app type, each with its own `FileSystemStorageAdapter` in an isolated subdirectory (e.g., `documents/text-editor/`, `documents/editor/`, `documents/drawing/`, `documents/task-board/`). Implement `GetRepo(appType)`, `ListDocuments(appType)` (scans storage directory for document IDs), `StartAsync`, `StopAsync` (flushes and disposes repos)
- [ ] Create `apps/Server.Api/src/Network/SignalRServerNetworkAdapter.cs` implementing `INetworkAdapter`. This adapter bridges SignalR hub events to the server Repo. Key methods: `OnClientConnected(connectionId, peerId, metadata)` → raises `PeerCandidate`, `OnClientDisconnected(connectionId, peerId)` → raises `PeerDisconnected`, `OnClientMessage(senderId, message)` → deserializes and raises `MessageReceived`. For outbound `Send(IRepoMessage)`: if message has `TargetPeerId`, look up the connection ID and send via `IHubContext`; otherwise broadcast to all connected clients. Maintain `ConcurrentDictionary<PeerId, ConnectionId>` mapping
- [ ] Wire `ServerRepoManager` and `SignalRServerNetworkAdapter` together: each Repo instance gets a `SignalRServerNetworkAdapter` as its network adapter and a `FileSystemStorageAdapter` as its storage adapter. The adapter needs `IHubContext<THub>` injected for sending messages back to clients
- [ ] Register `ServerRepoManager` and its dependencies in `Server.Api/Program.cs` DI container. Configure `ServerRepoOptions` from environment variable `SERVER_STORAGE_DIR` with fallback to default
- [ ] Add `/hubs/health` endpoint that reports status of all registered hubs and their Repo instances

## Sprint 5: App-Specific SignalR Hubs on Server

Create per-app hub classes that integrate with the server Repo via `SignalRServerNetworkAdapter`.

- [ ] Create `apps/Server.Api/src/Hubs/ICollaborativeHubClient.cs` extending `IDocumentsHubClient` with `Task OnDocumentListUpdated(List<string> documentIds)` for push-based document list updates
- [ ] Create `apps/Server.Api/src/Hubs/CollaborativeHub.cs` — abstract base class inheriting `Hub<ICollaborativeHubClient>`. Implement: `Handshake()` that notifies server adapter + broadcasts to others, `SendMessage()` that routes through server adapter (not direct relay), `SubscribeToDocument()`, `UnsubscribeFromDocument()`, `ListDocuments()` that returns document IDs from `ServerRepoManager`, `OnConnectedAsync/OnDisconnectedAsync` that notify server adapter. Each hub is configured with its `appType` string
- [ ] Create four concrete hub classes: `TextEditorHub`, `EditorHub`, `DrawingHub`, `TaskBoardHub` — each inheriting `CollaborativeHub` with its specific `appType` string ("text-editor", "editor", "drawing", "task-board")
- [ ] In `Server.Api/Program.cs`, map all four hub endpoints: `/hubs/text-editor`, `/hubs/editor`, `/hubs/drawing`, `/hubs/task-board`. Keep the legacy `/hubs/documents` endpoint for backward compatibility during migration
- [ ] Remove the old `AddSignalRDocumentsHub()` call and replace with per-hub SignalR configuration using `AddSignalR().AddMessagePackProtocol().AddJsonProtocol()`
- [ ] Build Server.Api and verify it starts successfully with all hubs mapped and health endpoint responding

## Sprint 6: Update Demo Apps — Hub Paths & Client RepoService

Update each demo app to connect to its app-specific hub and add server document listing.

- [ ] In `DemoApp.Shared/src/RepoService.cs`, add a `HubPath` property to `RepoServiceOptions` (e.g., `/hubs/text-editor`). Update `InitializeAsync` to construct the full hub URL from Aspire service discovery base URL + `HubPath`. Add `ListServerDocumentsAsync()` method that calls `connection.InvokeAsync<List<string>>("ListDocuments")` on the hub connection
- [ ] Update `DemoApp.CollaborativeTextEditor/Program.cs` to use hub path `/hubs/text-editor` in `ResolveHubUrl()`
- [ ] Update `DemoApp.CollaborativeEditor/Program.cs` to use hub path `/hubs/editor`
- [ ] Update `DemoApp.CollaborativeDrawing/Program.cs` to use hub path `/hubs/drawing`
- [ ] Update `DemoApp.CollaborativeTaskBoard/Program.cs` to use hub path `/hubs/task-board`
- [ ] Add `GetHubConnection()` or expose the underlying `HubConnection` from `RepoService`/`SignalRConnectionService` so that `ListServerDocumentsAsync()` can invoke the `ListDocuments` RPC

## Sprint 7: Update DocumentSessionDialog — Server Document Discovery

Enhance the DocumentSessionDialog to show both local and server documents.

- [ ] In `DocumentSessionDialogViewModel.cs`, add a `ServerDocuments` collection (`ObservableCollection<string>`), an `AllDocuments` collection that merges local and server documents, and a `DocumentSource` enum (Local, Server, Both) to tag each entry
- [ ] Add `LoadServerDocumentsAsync(RepoService repoService)` method that calls `repoService.ListServerDocumentsAsync()` and populates `ServerDocuments`. Handle connection failures gracefully (show only local documents if server unreachable)
- [ ] Update the merged document list to show visual indicators: filled circle for server documents, open circle for local-only documents. Show "(also local)" label for documents that exist in both
- [ ] Update `DocumentSessionDialog.axaml` to display the merged document list with the new visual indicators. Add a "Refresh" button to re-fetch server documents
- [ ] Update all four demo app windows to call `LoadServerDocumentsAsync()` after initializing `RepoService` (before showing the dialog)
- [ ] Build and run `DemoApp.CollaborativeTextEditor` via Aspire. Use `six-avalonia-mcp` to verify the dialog shows server documents after the server is running. Create a document in one replica, verify it appears in the other replica's dialog server list

## Sprint 8: End-to-End Integration Testing & Polish

Validate the full client-server sync workflow across all four demo apps.

- [ ] Start `Infra.AppHost` and enable all four demo apps (remove `.WithExplicitStart()` temporarily). Verify all apps start successfully and connect to the server
- [ ] Test `CollaborativeTextEditor`: Create a document in replica 0, verify it appears in replica 1's document list. Open in replica 1, type text in both, verify changes sync bidirectionally through the server
- [ ] Test `CollaborativeEditor`: Same flow as TextEditor but also test rich text formatting (bold, italic) syncing correctly between replicas
- [ ] Test `CollaborativeDrawing`: Create a canvas in one replica, add shapes, verify they appear in the other replica. Move/resize shapes and verify sync
- [ ] Test `CollaborativeTaskBoard`: Create columns and tasks, verify they sync. Test task movement between columns. Test vote counter sync (CRDT counter)
- [ ] Test offline/reconnection: Start editing in one replica, stop the server, continue editing locally, restart the server, verify changes sync automatically when reconnected
- [ ] Restore `.WithExplicitStart()` on apps that had it before (CollaborativeEditor, CollaborativeDrawing, CollaborativeTaskBoard). Verify the `AppHost.cs` is in its final correct state
- [ ] Review all console output for errors, warnings, or unnecessary debug logging. Clean up any leftover `Console.WriteLine` debug statements in production code paths
