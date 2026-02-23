# Implementation Plan: Multi-Client Sync Demo Apps

## Sprint 1: Server Infrastructure & SignalR Hub Setup

Set up the server-side SignalR hub and Aspire orchestration so demo apps have something to connect to.

- [ ] **Update Server.Api to host SignalR DocumentsHub**: Add `builder.Services.AddSignalRDocumentsHub()` and `app.MapHub<DocumentsHub>("/hubs/documents")` to `apps/Server.Api/src/Program.cs`. Add project reference to `Six.Automerge.Repo.Network.SignalR.Server`. Update CORS policy to use `SetIsOriginAllowed(_ => true)` with `AllowCredentials()` (required for SignalR WebSocket). Add a `/hubs/health` health check endpoint.
- [ ] **Update Aspire AppHost to register all four demo apps**: Modify `apps/Infra.AppHost/src/AppHost.cs` to add `DemoApp.CollaborativeTextEditor`, `DemoApp.CollaborativeEditor`, `DemoApp.FigmaClone`, and `DemoApp.TaskBoard` as projects. Each with `WithReference(api)`, `WaitFor(api)`, `WithExplicitStart()`, `WithReplicas(2)`, and `ExcludeFromManifest()`. Add project references to AppHost `.csproj`. Remove or keep the existing `DemoApp.Avalonia` entry (it's the old empty shell).
- [ ] **Add Aspire ServiceDefaults to each demo app**: Add `<ProjectReference>` to `Infra.ServiceDefaults` in each demo app's `.csproj`. Call `builder.AddServiceDefaults()` in each app's startup (for Avalonia apps this means reading Aspire env vars for service discovery, not the full ASP.NET Core builder). Each app must read `services__api__https__0` or `services__api__http__0` environment variable to discover the hub URL.
- [ ] **Build and verify**: Run `dotnet build` from repo root to ensure Server.Api compiles with SignalR hub. Run `dotnet run --project apps/Infra.AppHost` to verify Aspire dashboard starts and shows all resources. Verify the SignalR hub is accessible (health check at `/hubs/health` or by connecting a test client).

## Sprint 2: Shared DemoApp Infrastructure (DemoApp.Shared project)

Create the shared project with RepoService and ConnectionStatusBar that all four apps will use.

- [ ] **Create `apps/DemoApp.Shared/` project**: New class library project targeting `net10.0` with Avalonia dependencies. Add project references to `Six.Automerge.Repo`, `Six.Automerge.Repo.Network.SignalR.Client`, `Six.Automerge.Repo.Storage.FileSystem`. Follow existing code style (StyleCop, file-scoped namespaces, 2-space indent). Add `GlobalUsings.cs` matching other app projects.
- [ ] **Implement `RepoService`**: Create `src/RepoService.cs` — lifecycle manager for Repo + SignalRNetworkAdapter + FileSystemStorageAdapter. Constructor takes `RepoServiceOptions` (hubUrl, storageBaseDir, appName). `InitializeAsync()`: creates `FileSystemStorageAdapter` (base dir = `~/.rayautomerge/{appName}/`), creates `SignalRConnectionService` + `SignalRNetworkAdapter`, creates `Repo(new RepoConfig { Storage = storage, NetworkAdapters = [adapter] })`, connects adapter. Exposes: `PeerId`, `ConnectionState`, `PeerCount`, `Repo`. Events: `ConnectionStateChanged`, `PeerCountChanged`. Implements `IDisposable` for cleanup. Create `RepoServiceOptions` record.
- [ ] **Implement `ConnectionStatusBar` control**: Create `src/Controls/ConnectionStatusBar.axaml` + `.axaml.cs` — reusable Avalonia UserControl. Shows: connection state indicator (colored dot), PeerId (truncated), peer count, document ID with copy button, server URL. All values bound via `StyledProperty` or `DataContext` binding. Create `ConnectionStatusBarViewModel` with observable properties.
- [ ] **Implement `DocumentSessionDialog` control**: Create `src/Controls/DocumentSessionDialog.axaml` + `.axaml.cs` — startup dialog for Create/Join/Resume. Shows: "Create New Document" button, document ID text input + "Join" button, list of previously stored documents (from FileSystem storage directory enumeration) with "Resume" buttons. Returns selected action to the app.
- [ ] **Add `DemoApp.Shared` reference to all four demo apps**: Update `.csproj` files for all four apps to reference `DemoApp.Shared`. Verify all projects build cleanly.

## Sprint 3: CollaborativeTextEditor — Migrate to Real Sync

Migrate the simplest app first as a reference implementation for the other three.

- [ ] **Remove old sync infrastructure from CollaborativeTextEditor**: Delete `Services/SyncService.cs`. Remove all SyncService references from `TextEditorViewModel.cs`. Remove the peer text pane from `MainWindow.axaml` (the right-side read-only TextBox). Remove "Simulate Peer Edit" and "Toggle Sync" buttons. Remove `PeerText` property from ViewModel.
- [ ] **Integrate RepoService into CollaborativeTextEditor**: Add `RepoService` initialization in `App.axaml.cs` or a startup service. Read hub URL from Aspire env vars (`services__api__https__0`) with fallback to configurable default. Pass to `RepoService.InitializeAsync()`. Wire `TextEditorViewModel` to use `RepoService` instead of `SyncService`.
- [ ] **Implement document lifecycle in CollaborativeTextEditor**: On app start, show `DocumentSessionDialog`. On "Create": `repo.Create<object>()` → initialize text CRDT → display editor. On "Join": `repo.Find<object>(docId)` → wait for document → read text → display editor. On "Resume": load from storage → display editor. Wire `AutomergeService` to work with `DocHandle`'s underlying `AutomergeDocument`.
- [ ] **Implement remote change handling for CollaborativeTextEditor**: Subscribe to `DocHandle.HeadsChanged` event. On remote change: marshal to UI thread, re-read text via `AutomergeService.GetText()`, update `DocumentText` property while preserving caret position (save caret index before update, restore after, clamping to new text length). Commit local changes on each text edit (debounced via Repo's `SaveDebounceRate`).
- [ ] **Update CollaborativeTextEditor UI layout**: Single full-width TextBox for editing. Add `ConnectionStatusBar` at the bottom. Toolbar keeps "Save" and adds "Export JSON". Status bar shows cursor position + character count. Update `MainWindow.axaml` layout.
- [ ] **Build, run, and test CollaborativeTextEditor with `six-avalonia-mcp`**: Build the full Aspire stack. Start Server.Api from Aspire dashboard. Start 2 CollaborativeTextEditor replicas. Create a document in instance 1, copy document ID, join from instance 2. Type text in instance 1, verify it appears in instance 2. Type text in instance 2, verify it appears in instance 1. Take screenshots to verify UI. Check connection status bar shows correct state.

## Sprint 4: CollaborativeEditor (Rich Text) — Migrate to Real Sync

Apply the same pattern from Sprint 3 to the rich text editor.

- [ ] **Remove old sync infrastructure from CollaborativeEditor**: Delete `Services/SyncService.cs`. Remove SyncService references from `EditorViewModel.cs`. Remove peer text pane from `EditorWindow.axaml`. Remove "Simulate Peer Edit" and "Toggle Sync" buttons.
- [ ] **Integrate RepoService and document lifecycle into CollaborativeEditor**: Same pattern as CollaborativeTextEditor. Add RepoService initialization, DocumentSessionDialog on startup, Create/Join/Resume flows. Wire `AutomergeService` to DocHandle's document.
- [ ] **Implement remote change handling for CollaborativeEditor**: Subscribe to `DocHandle.HeadsChanged`. On remote change: re-read text via `AutomergeService.GetText()` or `GetSpans()`, update editor text preserving caret. Re-read formatting state via `AutomergeService.GetMarks()` to update toolbar toggles. Formatting operations (bold, italic, etc.) commit to document → auto-sync.
- [ ] **Update CollaborativeEditor UI layout**: Single full-width editor area. Formatting toolbar (Bold, Italic, Underline, Link). ConnectionStatusBar at bottom. Remove peer preview pane.
- [ ] **Build, run, and test CollaborativeEditor with `six-avalonia-mcp`**: Start Aspire stack. Start 2 editor replicas. Create document, join from other instance. Test: type text → syncs. Apply bold formatting → syncs. Verify formatting state propagates correctly. Take screenshots.

## Sprint 5: FigmaClone — Migrate to Real Sync

Apply the sync pattern to the canvas-based app.

- [ ] **Remove old sync infrastructure from FigmaClone**: Delete `Services/SyncService.cs`. Remove SyncService references from `CanvasViewModel.cs`. Remove peer simulation methods (`SimulatePeerAddShape`, `SimulatePeerMoveShape`).
- [ ] **Integrate RepoService and document lifecycle into FigmaClone**: Add RepoService initialization with DocumentSessionDialog. Wire `AutomergeService` to DocHandle's document. On Create: initialize with default layer. On Join: wait for document, read canvas state.
- [ ] **Implement remote change handling for FigmaClone**: Subscribe to `DocHandle.HeadsChanged`. On remote change: call `AutomergeService.ReadCanvas()` to get full layer/shape state. Diff against current `Shapes` ObservableCollection — add new shapes, remove deleted shapes, update moved/resized/recolored shapes. Marshal all UI updates to dispatcher thread. Shape operations (add, move, resize, recolor, delete) → CRDT ops → commit → auto-sync.
- [ ] **Refactor undo mechanism in FigmaClone**: The current Fork-based undo won't work with Repo (Fork creates a separate document). Either remove undo for now (simplest) or implement undo via Automerge's change history. Recommend removing undo for this sprint and noting it as a future enhancement.
- [ ] **Update FigmaClone UI layout**: Keep existing canvas + toolbar + layer panel. Add ConnectionStatusBar at bottom. Remove any peer simulation UI elements.
- [ ] **Build, run, and test FigmaClone with `six-avalonia-mcp`**: Start Aspire stack + 2 FigmaClone replicas. Create canvas in instance 1, join from instance 2. Draw rectangle in instance 1 → appears in instance 2. Move shape in instance 2 → moves in instance 1. Add layer, toggle visibility, delete shape → all sync. Take screenshots.

## Sprint 6: TaskBoard — Add Sync from Scratch

Add sync capability to the TaskBoard app which currently has none.

- [ ] **Integrate RepoService and document lifecycle into TaskBoard**: Add DemoApp.Shared reference (already done in Sprint 2). Add RepoService initialization with DocumentSessionDialog. Wire `AutomergeService` to DocHandle's document. On Create: call `SeedDefaultColumns()` to initialize "To Do", "In Progress", "Done" columns. On Join: wait for document, read board state via `ReadBoard()`.
- [ ] **Implement remote change handling for TaskBoard**: Subscribe to `DocHandle.HeadsChanged`. On remote change: call `AutomergeService.ReadBoard()` to get full column/task state. Rebuild `Columns` ObservableCollection from CRDT state (diff or full rebuild). Vote counter uses Automerge Counter type — merges automatically on sync. Marshal all updates to dispatcher thread.
- [ ] **Wire all TaskBoard operations to commit + sync**: Column add/remove/rename → CRDT ops → `doc.Commit()`. Task add/remove/move/upvote → CRDT ops → `doc.Commit()`. Each commit triggers Repo's sync protocol automatically.
- [ ] **Update TaskBoard UI layout**: Keep existing Kanban layout (horizontal scrolling columns with task cards). Add ConnectionStatusBar at bottom. All existing functionality preserved (add column, add task, upvote, delete).
- [ ] **Build, run, and test TaskBoard with `six-avalonia-mcp`**: Start Aspire stack + 2 TaskBoard replicas. Create board in instance 1 (seeds default columns), join from instance 2. Add task in instance 1 → appears in instance 2. Upvote task in both instances → vote count merges (CRDT counter). Move task between columns → syncs. Take screenshots.

## Sprint 7: End-to-End Validation & Polish

Final round of testing, bug fixing, and polish across all four apps.

- [ ] **Full E2E test: all four apps running simultaneously**: Start Aspire stack. Start 2 replicas of each app (8 total app instances). Verify each app can create and join documents independently. Verify sync works within each app type. Take screenshots of each app pair showing synced state.
- [ ] **Test persistence: restart and resume**: Create document in each app, make changes, close app. Restart app → verify "Resume" option appears in DocumentSessionDialog. Resume → verify document loads from FileSystem storage. Connect to server → verify sync resumes with other peers.
- [ ] **Test disconnection and reconnection**: Start 2 app replicas with active sync. Stop Server.Api from Aspire dashboard. Verify apps show "Disconnected" state in ConnectionStatusBar. Make local changes while disconnected. Restart Server.Api. Verify apps reconnect and sync pending changes.
- [ ] **Fix any bugs discovered during E2E testing**: Address sync issues, UI glitches, race conditions, or state management problems found during testing. Ensure all four apps are stable and functional.
- [ ] **Clean up old DemoApp.Avalonia**: Remove or repurpose the empty `apps/DemoApp.Avalonia/` project (it was the old Aspire-integrated shell that is now replaced by the four demo apps). Remove its reference from AppHost if still present. Clean up any orphaned references.
