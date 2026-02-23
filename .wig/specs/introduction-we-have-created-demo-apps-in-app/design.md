# Design: Client-Server Collaborative Demo Apps

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│  Infra.AppHost (Aspire Orchestrator)                                 │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Server.Api (1 replica)                                        │  │
│  │  ┌──────────────┐ ┌──────────────┐ ┌───────────┐ ┌──────────┐│  │
│  │  │ TextEditorHub│ │  EditorHub   │ │DrawingHub │ │TaskBrdHub││  │
│  │  │  /hubs/      │ │  /hubs/      │ │ /hubs/    │ │ /hubs/   ││  │
│  │  │  text-editor │ │  editor      │ │ drawing   │ │task-board││  │
│  │  └──────┬───────┘ └──────┬───────┘ └─────┬─────┘ └────┬─────┘│  │
│  │         │                │               │             │      │  │
│  │  ┌──────▼────────────────▼───────────────▼─────────────▼─────┐│  │
│  │  │  ServerRepoManager (manages per-hub Repo instances)        ││  │
│  │  │  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐     ││  │
│  │  │  │  Repo    │  │  Repo   │  │  Repo   │  │  Repo   │     ││  │
│  │  │  │(TextEd)  │  │(Editor) │  │(Drawing)│  │(TaskBrd)│     ││  │
│  │  │  └────┬─────┘  └────┬────┘  └────┬────┘  └────┬────┘     ││  │
│  │  │       │              │            │            │           ││  │
│  │  │  ┌────▼──────────────▼────────────▼────────────▼─────┐    ││  │
│  │  │  │  FileSystemStorageAdapter (per-repo, isolated dirs)│    ││  │
│  │  │  │  documents/text-editor/   documents/editor/        │    ││  │
│  │  │  │  documents/drawing/       documents/task-board/     │    ││  │
│  │  │  └────────────────────────────────────────────────────┘    ││  │
│  │  └────────────────────────────────────────────────────────────┘│  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Demo App Replicas (2 each)                                    │  │
│  │  ┌──────────────────┐  ┌──────────────────┐                   │  │
│  │  │ TextEditor (0)   │  │ TextEditor (1)   │                   │  │
│  │  │ ┌──────────────┐ │  │ ┌──────────────┐ │                   │  │
│  │  │ │ Repo         │ │  │ │ Repo         │ │                   │  │
│  │  │ │ SignalRClient│ │  │ │ SignalRClient│ │                   │  │
│  │  │ │ FileSystem   │ │  │ │ FileSystem   │ │                   │  │
│  │  │ └──────────────┘ │  │ └──────────────┘ │                   │  │
│  │  └──────────────────┘  └──────────────────┘                   │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

## Key Design Decisions

### D1: Server as Repo Peer (Option A)

The server runs its own `Repo` instance for each app hub. This means:
- The server participates natively in the Automerge sync protocol
- No custom sync logic needed - reuse existing `Repo`, `CollectionSynchronizer`, `DocSynchronizer`
- Server persistence via the same `FileSystemStorageAdapter` used by clients
- New peer connections trigger automatic sync (existing behavior)

### D2: SignalRServerNetworkAdapter (New Component)

A new `SignalRServerNetworkAdapter` bridges the gap between the SignalR hub (which receives/sends messages from/to clients) and the server-side `Repo` (which needs an `INetworkAdapter` to participate in sync).

```
Client → SignalR Hub → SignalRServerNetworkAdapter → Server Repo
                                                          ↓
Server Repo → SignalRServerNetworkAdapter → SignalR Hub → Clients
```

### D3: Per-App Hub Isolation

Each app type gets its own hub class that inherits from a base `CollaborativeHub<TClient>`. This provides:
- Isolated document namespaces (drawing doc IDs don't conflict with text editor doc IDs)
- Isolated server Repo instances with separate storage directories
- Independent configuration and scaling
- Clean separation for future app-specific server logic

---

## Component Design

### 1. ServerRepoManager (New - Server.Api)

**File**: `apps/Server.Api/src/Services/ServerRepoManager.cs`

Manages the lifecycle of server-side Repo instances. Registered as a singleton `IHostedService`.

```csharp
public sealed class ServerRepoManager : IHostedService, IDisposable
{
  // One Repo per app type
  Dictionary<string, Repo> _repos;

  // Configuration
  ServerRepoOptions _options;

  Task StartAsync(CancellationToken ct)
  {
    // Create Repo instances for each registered app type
    // Each with its own FileSystemStorageAdapter
    // Storage dirs: {baseDir}/{appType}/
  }

  Task StopAsync(CancellationToken ct)
  {
    // Flush + shutdown all repos
  }

  Repo GetRepo(string appType);
  IReadOnlyList<string> ListDocuments(string appType);
}
```

**Configuration**:
```csharp
public sealed class ServerRepoOptions
{
  // Base storage directory, default: {AppContext.BaseDirectory}/documents/
  public string StorageBaseDirectory { get; set; }
}
```

### 2. SignalRServerNetworkAdapter (New - Server lib or Server.Api)

**File**: `apps/Server.Api/src/Network/SignalRServerNetworkAdapter.cs`

Implements `INetworkAdapter` for the server side. It is the bridge between SignalR hubs and server Repo.

```
class SignalRServerNetworkAdapter : INetworkAdapter
{
  // Called by the hub when a client sends a message
  void OnClientMessage(string connectionId, PeerId senderId, MessageDto message);

  // Called by the hub when a client connects (handshake)
  void OnClientConnected(string connectionId, PeerId peerId, PeerMetadata? metadata);

  // Called by the hub when a client disconnects
  void OnClientDisconnected(string connectionId, PeerId peerId);

  // INetworkAdapter.Send() → route to correct SignalR client(s)
  void Send(IRepoMessage message);

  // Holds reference to IHubContext for sending to clients
  IHubContext<THub, TClient> _hubContext;

  // Maps PeerId → ConnectionId for targeted sends
  ConcurrentDictionary<string, string> _peerConnections;
}
```

**Key behaviors**:
- `OnClientMessage` → raises `MessageReceived` event → Repo processes via sync protocol
- When Repo calls `Send()` (via sync protocol outbound) → adapter routes to correct SignalR client(s) via `IHubContext`
- `OnClientConnected` → raises `PeerCandidate` event → Repo initiates sync for all known documents
- `OnClientDisconnected` → raises `PeerDisconnected` event → Repo cleans up sync state

### 3. App-Specific Hub Classes (New - Server.Api)

Each hub inherits from the existing `DocumentsHub` pattern but integrates with the server Repo via `SignalRServerNetworkAdapter`.

**File**: `apps/Server.Api/src/Hubs/TextEditorHub.cs` (and similar for others)

```csharp
// Hub interfaces (new, in Abstractions or Server.Api)
public interface ICollaborativeHubClient : IDocumentsHubClient
{
  Task OnDocumentListUpdated(List<string> documentIds);
}

// Base hub with server Repo integration
public abstract class CollaborativeHub<TClient> : Hub<TClient>
  where TClient : class, ICollaborativeHubClient
{
  ServerRepoManager _repoManager;
  string _appType;

  // All existing DocumentsHub methods (Handshake, SendMessage, etc.)
  // BUT: messages are routed through the server Repo first

  async Task Handshake(PeerHandshakeDto handshake)
  {
    // Store peer info
    // Notify server Repo adapter about new peer
    // Broadcast to other clients
  }

  async Task SendMessage(MessageDto message)
  {
    // Route to server Repo adapter (server processes sync)
    // Server Repo adapter handles distribution to other clients
  }

  Task<List<string>> ListDocuments()
  {
    // Return document IDs from server storage
    return _repoManager.ListDocuments(_appType);
  }
}

// Concrete hubs
public class TextEditorHub : CollaborativeHub<ICollaborativeHubClient> { }
public class EditorHub : CollaborativeHub<ICollaborativeHubClient> { }
public class DrawingHub : CollaborativeHub<ICollaborativeHubClient> { }
public class TaskBoardHub : CollaborativeHub<ICollaborativeHubClient> { }
```

### 4. Updated Client RepoService (DemoApp.Shared)

**File**: `apps/DemoApp.Shared/src/RepoService.cs`

Minor changes:
- Hub URL now points to app-specific endpoint (e.g., `/hubs/text-editor`)
- Add `ListServerDocumentsAsync()` method that calls hub's `ListDocuments()`

```csharp
public class RepoService
{
  // Existing: HubUrl, Repo, NetworkAdapter, StorageAdapter

  // New: fetch document list from server
  async Task<List<string>> ListServerDocumentsAsync()
  {
    // Call hub.InvokeAsync<List<string>>("ListDocuments")
    // via the SignalRConnectionService's HubConnection
  }
}
```

### 5. Updated DocumentSessionDialog (DemoApp.Shared)

**File**: `apps/DemoApp.Shared/src/Controls/DocumentSessionDialog.axaml`

Enhanced to show both local and server documents:

```
┌──────────────────────────────────────────┐
│  Document Session                         │
│                                          │
│  [Create New Document]                   │
│                                          │
│  Join by ID: [________________] [Join]   │
│                                          │
│  ─── Available Documents ───             │
│                                          │
│  SERVER:                                 │
│  ● doc-abc123  (also local)    [Open]    │
│  ● doc-def456                  [Open]    │
│                                          │
│  LOCAL ONLY:                             │
│  ○ doc-ghi789                  [Open]    │
│                                          │
└──────────────────────────────────────────┘
```

The dialog fetches documents from both local storage and server, merges them, and presents with indicators.

---

## Data Flow

### Create Document Flow

```
1. User clicks "Create New"
2. Client: Repo.Create<T>() → generates DocumentId, creates AutomergeDocument
3. Client: Initialize document schema (content/layers/columns)
4. Client: Automerge sync protocol sends initial state to server
5. Server Repo: Receives sync, merges (first time = full doc), persists to FileSystem
6. Server Repo: Distributes to other connected clients via sync protocol
7. Other clients: Receive sync, create/update local copy
```

### Resume Document Flow (Client Has Local Copy)

```
1. User selects document from list
2. Client: Repo.Find<T>(documentId) → loads from local FileSystemStorageAdapter
3. Client: Sync protocol connects to server, exchanges sync messages
4. Server: Sends any changes client is missing
5. Client: Merges server changes into local document
6. Client: Displays merged document to user
```

### Join Document Flow (Client Has No Local Copy)

```
1. User enters document ID or selects server-only document
2. Client: Repo.Find<T>(documentId) → not in local storage
3. Client: Sync protocol requests document from server
4. Server: Sends document via sync protocol (or snapshot if sync state empty)
5. Client: Receives, persists locally, displays
```

### Offline Edit + Reconnection Flow

```
1. Client editing offline (server unreachable)
2. Changes accumulate locally, persisted to FileSystemStorageAdapter
3. Client reconnects (SignalR automatic reconnect)
4. Handshake triggers PeerCandidate event on server
5. Server initiates sync for all known documents
6. Automerge sync protocol exchanges only the missing changes (efficient)
7. Both client and server converge to same state
```

---

## Bug Fix Design

### FileSystemStorageAdapter.LoadRange() Deadlock Fix

**Root Cause**: `LoadRange()` holds `_lock`, then calls `Load()` which tries to re-acquire `_lock`.

**Fix**: Extract an internal `LoadUnlocked()` method that reads from cache/disk without acquiring the lock. `LoadRange()` calls `LoadUnlocked()` while holding the lock. `Load()` continues to acquire the lock and delegates to `LoadUnlocked()`.

```csharp
// Internal method that does not acquire lock
byte[]? LoadCore(StorageKey key)
{
  // Check cache
  // If miss, read from disk
  // Update cache
  return data;
}

public async Task<byte[]?> Load(StorageKey key)
{
  await _lock.WaitAsync();
  try { return LoadCore(key); }
  finally { _lock.Release(); }
}

public async Task<IReadOnlyList<Chunk>> LoadRange(StorageKey keyPrefix)
{
  await _lock.WaitAsync();
  try
  {
    foreach(var file in WalkDirectory(...))
    {
      var data = LoadCore(storageKey); // No deadlock!
      // ...
    }
  }
  finally { _lock.Release(); }
}
```

### RemoveRange Cache Fix

**Root Cause**: `RemoveRange()` deletes files but doesn't invalidate cache entries.

**Fix**: After deleting the directory tree, iterate through cache keys and evict any that match the removed prefix.

### Repo/DocHandle Sync Lock Fix

**Root Cause**: `_handleLock.Wait()` and similar synchronous waits block the UI thread.

**Fix Strategy**: For `Repo.cs`:
- Public methods called from UI (Create, Handles property) must not block the UI thread
- Convert synchronous `Create<T>()` to primarily use `CreateAsync<T>()`
- For the `Handles` property, either make it async-friendly or use a concurrent collection that doesn't need locking
- Replace `_handleLock.Wait()` with `_handleLock.WaitAsync()` where possible

For `CollectionSynchronizer.cs` and `DocSynchronizer.cs`:
- Replace `_lock.Wait()` with `_lock.WaitAsync()` in all methods
- Update callers to be async
- Ensure deferred event pattern is preserved

For `DocHandle.cs`:
- Replace `CompositeSemaphore` synchronous `.Wait()` with async alternatives
- This may require restructuring the `CompositeSemaphore` to support `await`

### Demo App async void Handler Fix

**Fix**: While `async void` is required for event handlers, wrap the body in try/catch with proper UI feedback. Ensure long operations use `ConfigureAwait(false)` where appropriate and dispatch UI updates via `Dispatcher.UIThread`.

---

## Server.Api Program.cs Changes

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register server repo manager
builder.Services.AddSingleton<ServerRepoManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ServerRepoManager>());
builder.Services.Configure<ServerRepoOptions>(options =>
{
  options.StorageBaseDirectory = Environment.GetEnvironmentVariable("SERVER_STORAGE_DIR")
    ?? Path.Combine(AppContext.BaseDirectory, "documents");
});

// Register per-hub SignalR adapters
builder.Services.AddSignalR()
  .AddMessagePackProtocol(...)
  .AddJsonProtocol();

builder.Services.AddCors(...);

var app = builder.Build();

// Map app-specific hubs
app.MapHub<TextEditorHub>("/hubs/text-editor");
app.MapHub<EditorHub>("/hubs/editor");
app.MapHub<DrawingHub>("/hubs/drawing");
app.MapHub<TaskBoardHub>("/hubs/task-board");

// Keep legacy hub for backwards compat during migration
app.MapHub<DocumentsHub>("/hubs/documents");

app.MapDefaultEndpoints();
```

---

## Aspire AppHost Changes

No changes needed to `AppHost.cs` - the existing `WithReference(api)` and service discovery already provide the correct base URL. Each demo app just needs to append the correct hub path.

---

## File Structure (New/Modified)

```
apps/Server.Api/
  src/
    Program.cs                          (MODIFIED)
    Services/
      ServerRepoManager.cs              (NEW)
      ServerRepoOptions.cs              (NEW)
    Network/
      SignalRServerNetworkAdapter.cs     (NEW)
    Hubs/
      CollaborativeHub.cs               (NEW - base class)
      TextEditorHub.cs                  (NEW)
      EditorHub.cs                      (NEW)
      DrawingHub.cs                     (NEW)
      TaskBoardHub.cs                   (NEW)
      ICollaborativeHubClient.cs        (NEW)

apps/DemoApp.Shared/
  src/
    RepoService.cs                      (MODIFIED - app-specific hub paths)
    Controls/
      DocumentSessionDialog.axaml       (MODIFIED - server document list)
      DocumentSessionDialogViewModel.cs (MODIFIED - server document fetch)
      ConnectionStatusBar.axaml         (MODIFIED - minor)

apps/DemoApp.CollaborativeTextEditor/
  Program.cs                            (MODIFIED - hub path)
  src/Views/MainWindow.axaml.cs         (MODIFIED - async fixes)

apps/DemoApp.CollaborativeEditor/
  Program.cs                            (MODIFIED - hub path)
  src/Views/EditorWindow.axaml.cs       (MODIFIED - async fixes)

apps/DemoApp.CollaborativeDrawing/
  Program.cs                            (MODIFIED - hub path)
  src/Views/MainWindow.axaml.cs         (MODIFIED - async fixes)

apps/DemoApp.CollaborativeTaskBoard/
  Program.cs                            (MODIFIED - hub path)
  src/Views/MainWindow.axaml.cs         (MODIFIED - async fixes)

libs/Six.Automerge.Repo.Storage.FileSystem/
  src/FileSystemStorageAdapter.cs       (MODIFIED - deadlock + cache fix)

libs/Six.Automerge.Repo/
  src/Repo.cs                           (MODIFIED - async lock fixes)
  src/DocHandle.cs                      (MODIFIED - CompositeSemaphore fix)
  src/Synchronizer/
    CollectionSynchronizer.cs           (MODIFIED - async lock fixes)
    DocSynchronizer.cs                  (MODIFIED - async lock fixes)
```
