# Design: DemoApp.CollaborativeEditor Issues 3

## 1. Replace RichTextEditor with Plain TextBox

### Current Architecture (Broken)

```
EditorWindow.axaml
  └── RichTextEditor (UserControl)
        ├── InputTextBox (TextBox, Foreground=Transparent) — captures keystrokes
        └── DisplayTextBlock (TextBlock, IsHitTestVisible=False) — renders formatted Inlines
```

The dual-layer approach causes selection rendering misalignment because the transparent TextBox and the overlay TextBlock have slightly different text layout metrics.

### New Architecture

```
EditorWindow.axaml
  └── TextBox (Name="LocalEditor", standard Avalonia TextBox)
        AcceptsReturn=True, TextWrapping=Wrap
        Text="{Binding DocumentText, Mode=TwoWay}"
```

**Key changes:**

| File | Change |
|------|--------|
| `EditorWindow.axaml` | Replace `<controls:RichTextEditor ...>` with `<TextBox ...>`. Remove `xmlns:controls` import. Hide formatting buttons with `IsVisible="False"`. Remove `Window.KeyBindings` for Ctrl+B/I/U. |
| `EditorWindow.axaml.cs` | Replace `RichTextEditor`-specific property subscriptions with `TextBox.CaretIndexProperty` / `TextBox.SelectionStartProperty` / `TextBox.SelectionEndProperty`. Remove keyboard shortcut handler (`OnEditorKeyDown`). Remove `ExecuteFormatCommand` calls from button click handlers. Remove `IsEditorFocused` check (TextBox provides this natively). |
| `EditorViewModel.cs` | No changes needed. `DocumentText` two-way binding works with plain TextBox. `FormattedText` property remains but is unused. Formatting commands remain but are never invoked from UI. |
| `RichTextEditor.axaml/.cs` | Keep files. No changes. They compile but are unused. |
| `FormattedTextRenderer.cs` | Keep file. No changes. |
| `FormattingState.cs` | Keep file. No changes. |

### TextBox Binding Details

The plain TextBox binds directly to `EditorViewModel.DocumentText`:

```xml
<TextBox Name="LocalEditor"
         AcceptsReturn="True"
         AcceptsTab="True"
         TextWrapping="Wrap"
         Text="{Binding DocumentText, Mode=TwoWay}"
         Watermark="Start typing here..."
         Margin="8"/>
```

Caret/selection tracking in `EditorWindow.axaml.cs`:

```csharp
// Wire TextBox properties directly (no RichTextEditor wrapper needed)
LocalEditor.PropertyChanged += (_, args) =>
{
  if(DataContext is not EditorViewModel vm) return;

  if(args.Property == TextBox.CaretIndexProperty)
    vm.CursorPosition = LocalEditor.CaretIndex;

  if(args.Property == TextBox.SelectionStartProperty ||
     args.Property == TextBox.SelectionEndProperty)
  {
    vm.SelectionLength = Math.Abs(LocalEditor.SelectionEnd - LocalEditor.SelectionStart);
  }
};
```

---

## 2. Per-Replica Storage Isolation

### Current Problem

All replicas use: `~/.rayautomerge/CollaborativeEditor/`

When 4 replicas start simultaneously:
- They all read/write the same document files
- FileSystemStorageAdapter uses file locks and caching
- Concurrent file access causes crashes and data corruption

### Solution: AppHost Injects Replica Index

**AppHost changes** — Replace `.WithReplicas(N)` with explicit per-resource loop:

```csharp
// infra/Infra.AppHost/AppHost.cs
int editorReplicas = 3;
for(int i = 0; i < editorReplicas; i++)
{
  builder.AddProject<Projects.DemoApp_CollaborativeEditor>($"collaborative-editor-{i}")
    .WithReference(api)
    .WaitFor(api)
    .WithEnvironment("REPLICA_INDEX", i.ToString())
    .ExcludeFromManifest();
}
```

**RepoService changes** — Read `REPLICA_INDEX` and incorporate into storage path:

```csharp
// apps/DemoApp.Shared/src/RepoService.cs
public string StorageDirectory
{
  get
  {
    string baseDir = _options.StorageBaseDir
      ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayautomerge",
        _options.AppName);

    string? replicaIndex = Environment.GetEnvironmentVariable("REPLICA_INDEX");
    if(!string.IsNullOrEmpty(replicaIndex))
    {
      return Path.Combine(baseDir, $"replica-{replicaIndex}");
    }

    return baseDir;
  }
}
```

**Result:**
- Replica 0: `~/.rayautomerge/CollaborativeEditor/replica-0/`
- Replica 1: `~/.rayautomerge/CollaborativeEditor/replica-1/`
- Replica 2: `~/.rayautomerge/CollaborativeEditor/replica-2/`
- Standalone (no env var): `~/.rayautomerge/CollaborativeEditor/` (unchanged)

### Impact on Other Demo Apps

The same pattern should be applied to `DemoApp.CollaborativeDrawing` and `DemoApp.CollaborativeTaskBoard` since they also use `.WithReplicas()` in AppHost. The `RepoService.StorageDirectory` fix is shared via `DemoApp.Shared`, so it benefits all apps automatically. Only AppHost needs per-app loop changes.

---

## 3. Server-Side Peer Tracking & Peer Count Display

### Current Problem

```
Client A ──SignalR──> Server (SyncServer)
Client B ──SignalR──> Server (SyncServer)
Client C ──SignalR──> Server (SyncServer)
```

Each client's `Repo` sees exactly 1 peer: the server. So `RepoService.PeerCount` = 1 for all clients. This is architecturally correct (star topology) but the UI should show the count of OTHER clients, not direct Repo peers.

### Solution: Document-Level Peer Tracking on Server

#### 3.1 New Server Component: `DocumentPeerTracker`

A new service in `Server.Api` that tracks which peers are subscribed to which documents.

```
Server.Api/src/Services/DocumentPeerTracker.cs
```

```csharp
public sealed class DocumentPeerTracker
{
  // documentId → set of PeerIds subscribed to it
  readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PeerInfoDto>> _documentPeers = new();

  // peerId → set of documentIds they're subscribed to (for cleanup on disconnect)
  readonly ConcurrentDictionary<string, HashSet<string>> _peerDocuments = new();

  public void AddPeerToDocument(string documentId, PeerInfoDto peer) { ... }
  public void RemovePeerFromDocument(string documentId, string peerId) { ... }
  public void RemovePeerFromAllDocuments(string peerId) { ... }
  public List<PeerInfoDto> GetDocumentPeers(string documentId, string? excludePeerId = null) { ... }
  public int GetDocumentPeerCount(string documentId, string? excludePeerId = null) { ... }
}
```

Registered as singleton in DI.

#### 3.2 New DTO: `PeerInfoDto`

Add to `Six.Automerge.Repo.Network.SignalR.Abstractions`:

```csharp
[MessagePackObject]
public sealed class PeerInfoDto
{
  [Key(0)] public required string PeerId { get; init; }
  [Key(1)] public PeerMetadataDto? Metadata { get; init; }
}
```

#### 3.3 Hub Interface Changes

**IDocumentsHub** (new methods):

```csharp
Task<List<PeerInfoDto>> GetDocumentPeers(string documentId);
Task<int> GetDocumentPeerCount(string documentId);
```

**IDocumentsHubClient** (new callback):

```csharp
Task OnDocumentPeersChanged(string documentId, List<PeerInfoDto> peers);
```

#### 3.4 DocumentsHub Integration

When a peer subscribes to a document (via `SendMessage` auto-subscribe or `SubscribeToDocument`):

1. Add peer to `DocumentPeerTracker`
2. Get updated peer list
3. Broadcast `OnDocumentPeersChanged` to all peers in the group

When a peer disconnects (`OnDisconnectedAsync`):

1. Remove peer from all documents in `DocumentPeerTracker`
2. For each affected document, broadcast `OnDocumentPeersChanged`

When `GetDocumentPeers` / `GetDocumentPeerCount` is called:

1. Look up calling peer's ID from `Context.Items["PeerId"]`
2. Query `DocumentPeerTracker`, excluding the calling peer
3. Return result

#### 3.5 Client-Side: SignalRNetworkAdapter Changes

The `SignalRNetworkAdapter` needs to handle the new `OnDocumentPeersChanged` callback:

```csharp
// New event
public event EventHandler<DocumentPeersChangedEventArgs>? DocumentPeersChanged;

// In ConnectAsyncInternal, register callback:
connection.On<string, List<PeerInfoDto>>("OnDocumentPeersChanged",
  (documentId, peers) =>
  {
    DocumentPeersChanged?.Invoke(this, new DocumentPeersChangedEventArgs(documentId, peers));
    return Task.CompletedTask;
  });
```

Also add a method to query peers on demand:

```csharp
public async Task<List<PeerInfoDto>> GetDocumentPeersAsync(string documentId)
{
  var connection = await _connectionService.GetConnection();
  return await connection.InvokeAsync<List<PeerInfoDto>>("GetDocumentPeers", documentId);
}
```

#### 3.6 Client-Side: RepoService Changes

Add document peer tracking to `RepoService`:

```csharp
// New event
public event EventHandler<DocumentPeersChangedEventArgs>? DocumentPeersChanged;

// New property — count of OTHER clients on the same document
public int DocumentPeerCount { get; private set; }

// Wire the adapter's event in InitializeAsync
_networkAdapter.DocumentPeersChanged += OnDocumentPeersChanged;

// Query initial peers after joining a document
public async Task<int> QueryDocumentPeersAsync(string documentId) { ... }
```

#### 3.7 Client-Side: UI Updates

**ConnectionStatusBarViewModel** — The `PeerCount` property now represents other clients, not Repo peers. No structural changes needed, just the data source changes.

**EditorWindow.axaml.cs** — After document is loaded, query initial peer count and wire real-time updates:

```csharp
// After SetTextDocument:
if(repoService?.NetworkAdapter is not null)
{
  var peers = await repoService.GetDocumentPeersAsync(documentId);
  _statusBarVm.PeerCount = peers.Count;
}
```

**EditorViewModel.UpdateConnectionStatus** — No structural change. The `peerCount` parameter now comes from server-reported document peers instead of Repo.PeerCount.

### Data Flow Diagram

```
Client A opens doc-123
  → Hub.SendMessage (auto-subscribes)
  → DocumentPeerTracker.AddPeerToDocument("doc-123", peerA)
  → Hub broadcasts OnDocumentPeersChanged("doc-123", []) to group
     (empty because only peerA so far, and it excludes self)

Client B opens doc-123
  → Hub.SendMessage (auto-subscribes)
  → DocumentPeerTracker.AddPeerToDocument("doc-123", peerB)
  → Hub broadcasts OnDocumentPeersChanged("doc-123", [peerA]) to Client B
  → Hub broadcasts OnDocumentPeersChanged("doc-123", [peerB]) to Client A

Client A UI shows: "Syncing (1 peer)"
Client B UI shows: "Syncing (1 peer)"

Client C opens doc-123
  → DocumentPeerTracker adds peerC
  → Hub broadcasts OnDocumentPeersChanged("doc-123", [peerA, peerB]) to Client C
  → Hub broadcasts OnDocumentPeersChanged("doc-123", [peerB, peerC]) to Client A
  → Hub broadcasts OnDocumentPeersChanged("doc-123", [peerA, peerC]) to Client B

All clients show: "Syncing (2 peers)"
```

### Component Diagram

```
┌──────────────────────────────────────────────────────────┐
│ Server.Api                                                │
│                                                          │
│  DocumentsHub ──→ DocumentPeerTracker (singleton)        │
│       │                  │                                │
│       │            tracks peers per document              │
│       │                  │                                │
│       ├─→ SyncServer (Automerge sync)                    │
│       └─→ SignalRServerTransport (message routing)       │
│                                                          │
│  New Hub Methods:                                        │
│    GetDocumentPeers(docId) → List<PeerInfoDto>           │
│    GetDocumentPeerCount(docId) → int                     │
│                                                          │
│  New Client Callback:                                    │
│    OnDocumentPeersChanged(docId, List<PeerInfoDto>)      │
└──────────────────────────────────────────────────────────┘
         ▲ ▲ ▲
         │ │ │
    ┌────┘ │ └────┐
    │      │      │
┌───┴──┐┌──┴──┐┌──┴───┐
│Rep 0 ││Rep 1││Rep 2 │  (isolated storage per replica)
│      ││     ││      │
│ Repo ││Repo ││ Repo │  (each sees server as 1 peer)
│      ││     ││      │
│ UI:  ││ UI: ││ UI:  │  (shows OTHER client count from
│ 2    ││ 2   ││ 2    │   server-reported DocumentPeers)
│peers ││peers││peers │
└──────┘└─────┘└──────┘
```

---

## Files to Modify

| File | Change Type | Description |
|------|-------------|-------------|
| `apps/DemoApp.CollaborativeEditor/src/Views/EditorWindow.axaml` | Modify | Replace RichTextEditor with TextBox, hide formatting toolbar |
| `apps/DemoApp.CollaborativeEditor/src/Views/EditorWindow.axaml.cs` | Modify | Simplify caret/selection wiring, remove formatting handlers |
| `apps/DemoApp.Shared/src/RepoService.cs` | Modify | Add replica-aware StorageDirectory, add document peer tracking |
| `apps/DemoApp.Shared/src/RepoServiceOptions.cs` | No change | |
| `infra/Infra.AppHost/AppHost.cs` | Modify | Replace WithReplicas with explicit loop + REPLICA_INDEX env var |
| `libs/Six.Automerge.Repo.Network.SignalR.Abstractions/src/IDocumentsHub.cs` | Modify | Add GetDocumentPeers, GetDocumentPeerCount methods |
| `libs/Six.Automerge.Repo.Network.SignalR.Abstractions/src/IDocumentsHubClient.cs` | Modify | Add OnDocumentPeersChanged callback |
| `libs/Six.Automerge.Repo.Network.SignalR.Abstractions/src/Models/PeerInfoDto.cs` | New | PeerInfoDto MessagePack DTO |
| `apps/Server.Api/src/Services/DocumentPeerTracker.cs` | New | Server-side document-peer tracking service |
| `apps/Server.Api/src/Hubs/DocumentsHub.cs` | Modify | Integrate DocumentPeerTracker, add new hub methods |
| `libs/Six.Automerge.Repo.Network.SignalR.Client/src/SignalRNetworkAdapter.cs` | Modify | Handle OnDocumentPeersChanged callback, add query method |

## Files to Keep (Unchanged, Hidden)

| File | Status |
|------|--------|
| `apps/DemoApp.CollaborativeEditor/src/Controls/RichTextEditor.axaml` | Keep, unused |
| `apps/DemoApp.CollaborativeEditor/src/Controls/RichTextEditor.axaml.cs` | Keep, unused |
| `apps/DemoApp.CollaborativeEditor/src/Controls/FormattedTextRenderer.cs` | Keep, unused |
| `apps/DemoApp.CollaborativeEditor/src/Models/FormattingState.cs` | Keep, unused |
