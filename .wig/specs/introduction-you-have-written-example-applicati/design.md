# Design: Multi-Client Sync Demo Apps

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  .NET Aspire AppHost (orchestrator)                              │
│  ├── Azure Storage Emulator (Azurite) - Orleans clustering       │
│  ├── Server.Silo (Orleans grain host)                            │
│  ├── Server.Api (SignalR hub + REST endpoints)                   │
│  ├── DemoApp.CollaborativeEditor (2 replicas, explicit start)    │
│  ├── DemoApp.CollaborativeTextEditor (2 replicas, explicit start)│
│  ├── DemoApp.FigmaClone (2 replicas, explicit start)             │
│  └── DemoApp.TaskBoard (2 replicas, explicit start)              │
└─────────────────────────────────────────────────────────────────┘

Each demo app instance:
┌───────────────────────────────────────────────────┐
│  Avalonia UI (Views + ViewModels)                  │
├───────────────────────────────────────────────────┤
│  AutomergeService (app-specific CRDT operations)   │
├───────────────────────────────────────────────────┤
│  RepoService (shared across all apps)              │
│  ├── Repo (document lifecycle)                     │
│  ├── SignalRNetworkAdapter (→ Server.Api hub)       │
│  └── FileSystemStorageAdapter (local persistence)  │
└───────────────────────────────────────────────────┘
```

## New Shared Component: RepoService

A new shared service class that encapsulates Repo + network + storage setup. This will live in a new shared project `apps/DemoApp.Shared/` so all four apps can reference it without code duplication.

### Project: `apps/DemoApp.Shared/DemoApp.Shared.csproj`

```
DemoApp.Shared/
├── DemoApp.Shared.csproj
└── src/
    ├── RepoService.cs              # Repo lifecycle management
    ├── RepoServiceOptions.cs       # Configuration record
    └── Controls/
        └── ConnectionStatusBar.axaml/.cs  # Shared connection status UI control
```

### RepoService Design

```csharp
public sealed class RepoService : IDisposable
{
  // Properties
  PeerId PeerId { get; }
  string? CurrentDocumentId { get; }
  ConnectionState ConnectionState { get; }  // Connecting, Connected, Disconnected, Reconnecting
  int PeerCount { get; }
  Repo Repo { get; }

  // Events
  event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
  event EventHandler<PeerCountChangedEventArgs>? PeerCountChanged;
  event EventHandler<DocumentChangedEventArgs>? RemoteDocumentChanged;

  // Methods
  Task InitializeAsync(string hubUrl);
  DocHandle<T> CreateDocument<T>(T? initialValue = default);
  Task<DocHandle<T>?> JoinDocument<T>(string documentId, TimeSpan? timeout = null);
  void Dispose();
}
```

### RepoServiceOptions

```csharp
public sealed record RepoServiceOptions
{
  // Hub URL - injected via Aspire service discovery or manual input
  string HubUrl { get; init; }

  // Storage base directory - defaults to app-specific path under user data dir
  string StorageBaseDirectory { get; init; }

  // App name - used for storage directory isolation
  string AppName { get; init; }
}
```

### ConnectionStatusBar Control

A reusable Avalonia UserControl showing:

```
┌────────────────────────────────────────────────────────────────┐
│ ● Connected | Peer: abc-1234 | Peers: 3 | Doc: XyZ...AbC [📋] │
│ Server: https://localhost:5001/hubs/documents                   │
└────────────────────────────────────────────────────────────────┘
```

- Green/yellow/red dot for connection state
- PeerId (truncated to 8 chars)
- Live peer count
- Document ID with copy-to-clipboard button
- Server URL (collapsible/tooltip)

Properties bound from the ViewModel:
- `ConnectionState` (enum)
- `PeerId` (string)
- `PeerCount` (int)
- `DocumentId` (string)
- `ServerUrl` (string)

### ConnectionStatusBar ViewModel

```csharp
public class ConnectionStatusBarViewModel : ViewModelBase
{
  // Observable properties
  string ConnectionStateText { get; }
  string ConnectionStateColor { get; }  // "#22C55E" green, "#EAB308" yellow, "#EF4444" red
  string PeerIdDisplay { get; }
  int PeerCount { get; }
  string DocumentIdDisplay { get; }
  string ServerUrl { get; }
  bool HasDocument { get; }

  // Commands
  ICommand CopyDocumentIdCommand { get; }
}
```

## Document Join/Create Flow

### Create Flow
1. App starts → Show "Create New" / "Join Existing" choice
2. User clicks "Create New"
3. `RepoService.CreateDocument<T>()` → generates DocumentId, creates document, returns `DocHandle<T>`
4. UI displays document ID (copiable)
5. Document auto-syncs via SignalR when other peers join

### Join Flow
1. App starts → Show "Create New" / "Join Existing" choice
2. User pastes document ID
3. `RepoService.JoinDocument<T>(docId)` → `Repo.Find<T>(docId)` → requests from peers
4. Document arrives via sync → handle becomes Ready
5. UI loads document state

### Resume Flow
1. App starts → Check FileSystem storage for existing documents
2. If found → Show "Resume [docId]" option alongside Create/Join
3. User clicks Resume → Load from storage, reconnect SignalR, resume sync

## UI: Document Session Dialog

A simple dialog shown on app startup (before the main editor UI):

```
┌──────────────────────────────────────┐
│  Welcome to [App Name]                │
│                                       │
│  [Create New Document]                │
│                                       │
│  ─── or ───                           │
│                                       │
│  Document ID: [________________] [Join]│
│                                       │
│  Previously opened:                   │
│  • XyZ...AbC (last modified 2h ago)   │
│    [Resume]                           │
└──────────────────────────────────────┘
```

## Per-App Modifications

### CollaborativeTextEditor

**Remove:**
- `SyncService.cs` (timer-based fork sync)
- Two-pane layout (local + peer views)
- "Simulate Peer Edit" / "Toggle Sync" buttons

**Modify `AutomergeService`:**
- Keep all CRDT operations unchanged (SpliceText, GetText, GetCursor, etc.)
- Add `LoadFromDocument(AutomergeDocument doc)` to initialize from a received document
- Add `OnRemoteChange()` callback that re-reads text state and fires events

**Modify `TextEditorViewModel`:**
- Hold reference to `RepoService` instead of `SyncService`
- On local text change → `AutomergeService.SpliceText(...)` → `doc.Commit(...)` → Repo auto-syncs
- On remote change → `AutomergeService.GetText()` → update `DocumentText` preserving caret position
- Expose connection properties for status bar binding

**UI Layout:**
```
┌────────────────────────────────────────┐
│ Toolbar: [Save] [Export JSON]          │
├────────────────────────────────────────┤
│                                        │
│  [TextBox - full width editor]         │
│                                        │
├────────────────────────────────────────┤
│ ConnectionStatusBar                     │
└────────────────────────────────────────┘
```

### CollaborativeEditor (Rich Text)

**Remove:**
- `SyncService.cs`
- Two-pane layout
- "Simulate Peer Edit" / "Toggle Sync" buttons

**Modify `AutomergeService`:**
- Same approach as CollaborativeTextEditor
- All mark/block operations unchanged
- Add remote change notification

**Modify `EditorViewModel`:**
- Replace `SyncService` with `RepoService`
- On format toggle → apply mark → commit → auto-sync
- On remote change → re-read spans/marks → update UI

**UI Layout:**
```
┌────────────────────────────────────────┐
│ Toolbar: [B][I][U][Link] [Save]        │
├────────────────────────────────────────┤
│                                        │
│  [TextBox - full width editor]         │
│                                        │
├────────────────────────────────────────┤
│ ConnectionStatusBar                     │
└────────────────────────────────────────┘
```

### FigmaClone (Canvas)

**Remove:**
- `SyncService.cs`
- Peer simulation methods

**Modify `AutomergeService`:**
- All shape/layer CRDT operations unchanged
- Add `ReadCanvas()` invocation on remote change notification

**Modify `CanvasViewModel`:**
- Replace fork-based undo with a simpler approach (or remove undo for now)
- On remote change → `ReadCanvas()` → diff with current ObservableCollection → update
- Shape moves/creates/deletes → CRDT ops → commit → auto-sync

**UI Layout:**
```
┌────────────────────────────────────────────────┐
│ Toolbar: [Rect][Ellipse] Colors [Delete][Save] │
├──────────────────────────────┬─────────────────┤
│                              │ Layers           │
│   [Canvas]                   │ ├── Layer 1      │
│                              │ └── Layer 2      │
│                              │                  │
├──────────────────────────────┴─────────────────┤
│ ConnectionStatusBar                             │
└────────────────────────────────────────────────┘
```

### TaskBoard (Kanban)

**Add:**
- Integration with `RepoService` (currently has no sync at all)

**Modify `AutomergeService`:**
- All column/task CRDT operations unchanged
- Add remote change notification

**Modify `BoardViewModel`:**
- Hold `RepoService` reference
- On remote change → `ReadBoard()` → rebuild columns/tasks ObservableCollections
- Column/task operations → CRDT ops → commit → auto-sync

**UI Layout:**
```
┌──────────────────────────────────────────────────────┐
│ Toolbar: [Add Column] [Save]                          │
├──────────────────────────────────────────────────────┤
│ ┌─────────┐ ┌─────────┐ ┌─────────┐                 │
│ │ To Do   │ │ In Prog │ │ Done    │                  │
│ │ ┌─────┐ │ │ ┌─────┐ │ │ ┌─────┐ │                 │
│ │ │Task1│ │ │ │Task2│ │ │ │Task3│ │                  │
│ │ └─────┘ │ │ └─────┘ │ │ └─────┘ │                 │
│ │ [+Add]  │ │ [+Add]  │ │ [+Add]  │                 │
│ └─────────┘ └─────────┘ └─────────┘                 │
├──────────────────────────────────────────────────────┤
│ ConnectionStatusBar                                   │
└──────────────────────────────────────────────────────┘
```

## Server.Api Modifications

Add SignalR hub mapping to `Program.cs`:

```csharp
// Add SignalR with MessagePack + LZ4
builder.Services.AddSignalRDocumentsHub();

// ... existing setup ...

// Map the hub
app.MapHub<DocumentsHub>("/hubs/documents");
```

CORS must be updated for SignalR with credentials:
```csharp
options.AddPolicy("AllowAll", policy =>
{
  policy
    .SetIsOriginAllowed(_ => true)  // Allow all origins but with credentials
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials();
});
```

## Aspire AppHost Modifications

Add all four demo apps with service discovery:

```csharp
// Existing: api with SignalR hub
var api = builder.AddProject<Projects.Server_Api>("api")
  .WithReference(orleansService.AsClient())
  .WithExternalHttpEndpoints()
  .WithReplicas(1)
  .WaitFor(silo);

// Add each demo app (2 replicas each for testing sync)
builder.AddProject<Projects.DemoApp_CollaborativeTextEditor>("collaborative-text-editor")
  .WithReference(api)
  .WaitFor(api)
  .WithExplicitStart()
  .WithReplicas(2)
  .ExcludeFromManifest();

builder.AddProject<Projects.DemoApp_CollaborativeEditor>("collaborative-editor")
  .WithReference(api)
  .WaitFor(api)
  .WithExplicitStart()
  .WithReplicas(2)
  .ExcludeFromManifest();

builder.AddProject<Projects.DemoApp_FigmaClone>("figma-clone")
  .WithReference(api)
  .WaitFor(api)
  .WithExplicitStart()
  .WithReplicas(2)
  .ExcludeFromManifest();

builder.AddProject<Projects.DemoApp_TaskBoard>("task-board")
  .WithReference(api)
  .WaitFor(api)
  .WithExplicitStart()
  .WithReplicas(2)
  .ExcludeFromManifest();
```

## Service Discovery Integration

Demo apps discover the hub URL from Aspire environment variables. Aspire injects `services__api__https__0` (or `http`) automatically via `WithReference(api)`.

In each app's startup:
```csharp
// Read hub URL from Aspire service discovery or fall back to manual config
var apiUrl = Environment.GetEnvironmentVariable("services__api__https__0")
          ?? Environment.GetEnvironmentVariable("services__api__http__0")
          ?? "https://localhost:5001";
var hubUrl = $"{apiUrl}/hubs/documents";
```

## FileSystem Storage Layout

Each app stores documents under a unique app-specific directory:

```
~/.rayautomerge/
  collaborative-text-editor/
    <docId-shard>/
      <docId>/
        snapshot/
        incremental/
        sync-state/
  collaborative-editor/
    ...
  figma-clone/
    ...
  task-board/
    ...
```

## Remote Change Handling Pattern

When a remote peer sends changes, the flow is:

1. SignalR hub routes `SyncMessage` to all subscribed peers
2. `SignalRNetworkAdapter.OnReceiveMessage()` fires `MessageReceived` event
3. `NetworkSubsystem` forwards to `Repo`
4. `CollectionSynchronizer` → `DocSynchronizer.ReceiveMessage()`
5. `AutomergeDocument.ReceiveSyncMessage()` merges changes
6. `DocHandle.HeadsChanged` event fires
7. App-specific handler reads new state from document
8. ViewModel updates UI on dispatcher thread

Each app subscribes to `DocHandle.HeadsChanged`:
```csharp
handle.HeadsChanged += (_, _) =>
{
  Dispatcher.UIThread.Post(() =>
  {
    // Re-read document state
    var text = automergeService.GetText();
    DocumentText = text;
  });
};
```

## Data Flow Diagram

```
User edits text in App Instance 1
         │
         ▼
AutomergeService.SpliceText(index, deleteCount, insertText)
         │
         ▼
AutomergeDocument.Commit("edit")
         │
         ▼
DocHandle fires HeadsChanged
         │
         ▼
Repo.ScheduleSave() ─────────────────────► FileSystemStorageAdapter.Save()
         │
         ▼
CollectionSynchronizer.NotifyDocumentChanged()
         │
         ▼
DocSynchronizer.GenerateSyncMessage() for each peer
         │
         ▼
SignalRNetworkAdapter.Send(SyncMessage)
         │
         ▼
SignalR Hub routes to document group (except sender)
         │
         ▼
App Instance 2: SignalRNetworkAdapter.OnReceiveMessage()
         │
         ▼
DocSynchronizer.ReceiveSyncMessage()
         │
         ▼
AutomergeDocument merges via CRDT
         │
         ▼
DocHandle.HeadsChanged event
         │
         ▼
ViewModel re-reads AutomergeService.GetText()
         │
         ▼
UI updates with merged text
```
