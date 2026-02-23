# 2026-02-21 00:06:14 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-1-task-6 | Sprint: Fix Critical Bugs — FileSystemStorageAdapter Deadlock & Cache

**Description**: Add a test for `RemoveRange()` verifying that cached data is cleared immediately (not stale until expiration)

## FileSystemStorageAdapter Bug Fixes (Sprint 1)
- Deadlock fix: Extracted `LoadCore()` private method for lock-free cache-then-disk reads. `Load()` wraps with lock, `LoadRange()` calls `LoadCore()` within its own lock scope.
- Cache staleness fix: Added `HashSet<string> _cacheKeys` field to track cache keys. `RemoveRange()` now evicts matching entries by prefix. `Save()`, `LoadCore()`, and `Remove()` all maintain this tracking set.
- Test count after fixes: Unit 20, Integration 9

# 2026-02-21 00:47:45 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-2-task-6 | Sprint: Fix Critical Bugs — Async Lock Fixes in Repo and Synchronizers

**Description**: Run `Repo.Tests.Unit` (259 tests) and `FFI.Tests.Unit` (145 tests) to verify no regressions from async refactoring

Sprint 2 async lock conversion: Repo.Handles property → GetHandlesAsync() method (properties can't be async). Create/Delete/Clone/Import all async. RegisterHandle must be called OUTSIDE _handleLock to prevent deadlocks (RegisterHandle → synchronizer → docSync.BeginSync → GetDocument callback → _handleLock). GetDocument/UpdateDocument callbacks stay sync (fast dict lookups). CompositeSemaphore → IAsyncDisposable with async factory. All callers updated: DemoApp.CollaborativeEditor, DemoApp.CollaborativeTextEditor, DemoApp.CollaborativeDrawing, DemoApp.CollaborativeTaskBoard, SignalR integration tests. Test counts post-sprint-2: Repo.Tests.Unit 260 pass, FFI.Tests.Unit 379 pass, FileSystem.Tests.Unit 20 pass.

# 2026-02-21 01:51:13 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-3-task-5 | Sprint: Fix Demo App UI Responsiveness

**Description**: Test the "Resume" flow by creating a document, closing the app, reopening, and clicking on the stored document. Verify no deadlock (this was the primary reproduction scenario for the `LoadRange` deadlock)

Sprint 3 UI responsiveness fixes: All 4 demo app windows (TextEditor, Editor, Drawing, TaskBoard) now dispatch all post-await ViewModel property updates to UI thread via Dispatcher.UIThread.InvokeAsync(). WhenReadyAsync() calls bounded with 30s timeout. TimeoutException catch added. RepoService cancellation support was already done in Sprint 2. Aspire AppHost was removed in refactoring — run Server.Api and demo apps separately. Tab navigation works for clicking unnamed template buttons in six-avalonia-mcp when coordinate-based clicks don't reach DataTemplate children.

# 2026-02-21 02:25:55 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-4-task-6 | Sprint: Server-Side Repo Infrastructure — ServerRepoManager & SignalRServerNetworkAdapter

**Description**: Add `/hubs/health` endpoint that reports status of all registered hubs and their Repo instances

SignalR does not support method overloading/hiding in derived hubs. Using `new` keyword on hub methods causes `System.NotSupportedException: Duplicate definitions of 'MethodName'`. Use `IHubFilter` pattern (`IHubFilter.InvokeMethodAsync`, `OnDisconnectedAsync`) to intercept hub method invocations server-side without subclassing the hub.

# 2026-02-21 03:09:59 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-6-task-5 | Sprint: Update Demo Apps — Hub Paths & Client RepoService

**Description**: Update `DemoApp.CollaborativeTaskBoard/Program.cs` to use hub path `/hubs/task-board`

Sprint 6: URL resolution centralized in RepoService.ResolveServerBaseUrl(). HubPath is the new required prop; HubUrl is optional override. Each demo app sets its own HubPath matching server MapHub paths. ListServerDocumentsAsync uses the HubConnection captured during InitializeAsync.

# 2026-02-21 04:06:17 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-7-task-6 | Sprint: Update DocumentSessionDialog — Server Document Discovery

**Description**: Build and run `DemoApp.CollaborativeTextEditor` via Aspire. Use `six-avalonia-mcp` to verify the dialog shows server documents after the server is running. Create a document in one replica, verify it appears in the other replica's dialog server list

Sprint 7: Server.Api native lib issue — `libsix_automerge.so` not copied transitively to Server.Api output. Manual copy needed: `cp libs/Six.Automerge.FFI/bin/Debug/net10.0/libsix_automerge.so apps/Server.Api/bin/Debug/net10.0/`. The `ExposeNativeLibraries` target in FFI.csproj uses `ReferenceCopyLocalPaths` + `ContentWithTargetPath` but these don't always propagate through 3+ levels of transitive ProjectReferences in MSBuild.

# 2026-02-21 04:06:17 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-7-task-6 | Sprint: Update DocumentSessionDialog — Server Document Discovery

**Description**: Build and run `DemoApp.CollaborativeTextEditor` via Aspire. Use `six-avalonia-mcp` to verify the dialog shows server documents after the server is running. Create a document in one replica, verify it appears in the other replica's dialog server list

Sprint 7: Server-side CollaborativeHubAdapter `SenderId is null` bug prevents server repo from relaying sync messages back to clients and persisting documents. This means `ListDocuments` returns empty on the server side. The dialog UI handles this gracefully (shows only local docs).

# 2026-02-21 06:55:21 UTC | Spec: introduction-we-have-created-demo-apps-in-app | Task: sprint-8-task-8 | Sprint: End-to-End Integration Testing & Polish

**Description**: Review all console output for errors, warnings, or unnecessary debug logging. Clean up any leftover `Console.WriteLine` debug statements in production code paths

Aspire DCP proxy holds ports even after managed process dies. The DCP API is at the kubeconfig server URL with bearer token auth. Executables can be listed at `/apis/usvc-dev.developer.microsoft.com/v1/executables`. To restart a dead resource, the process must be started on the DCP's internal ports (found in the executable's `effectiveEnv` → `ASPNETCORE_URLS`), not the proxy ports. However, the DCP proxy itself doesn't resume forwarding to a manually restarted process.

