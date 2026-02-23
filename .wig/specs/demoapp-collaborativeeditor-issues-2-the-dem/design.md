# Design: DemoApp.CollaborativeEditor Refactoring

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│  DemoApp.CollaborativeEditor (Avalonia UI)                  │
│  ┌───────────────────────┐  ┌────────────────────────────┐  │
│  │ EditorViewModel       │  │ RichTextEditor (Control)   │  │
│  │ Uses TextDocument API │  │ Renders FormattedText      │  │
│  └───────────┬───────────┘  └────────────────────────────┘  │
├──────────────┼──────────────────────────────────────────────┤
│  Six.Automerge.TextEdit (NEW LIBRARY)                       │
│  ┌───────────┴───────────┐  ┌────────────────────────────┐  │
│  │ TextDocument           │  │ FormattedText model        │  │
│  │ Wraps DocHandle<T>    │  │ FormattedSpan[]            │  │
│  │ Diff/Splice engine    │  │ MarkInfo                   │  │
│  │ Change debouncing     │  │ CursorState                │  │
│  └───────────┬───────────┘  └────────────────────────────┘  │
├──────────────┼──────────────────────────────────────────────┤
│  Six.Automerge.Repo (EXISTING — with sync throttling added) │
│  ┌───────────┴───────────┐  ┌────────────────────────────┐  │
│  │ Repo + DocHandle<T>   │  │ DocSynchronizer            │  │
│  │ Document lifecycle    │  │ + SyncDebounceRate (NEW)   │  │
│  └───────────┬───────────┘  └────────────────────────────┘  │
├──────────────┼──────────────────────────────────────────────┤
│  Six.Automerge (FFI bindings)                               │
│  AutomergeDocument, SpliceText, Mark, GetSpans, etc.        │
└──────────────┼──────────────────────────────────────────────┘
               │
┌──────────────┴──────────────────────────────────────────────┐
│  Server.Api (uses Six.Automerge.SyncServer)                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Six.Automerge.SyncServer (NEW LIBRARY)                 │ │
│  │ SyncServer class — authoritative Repo per app-type     │ │
│  │ Debounced persistence (5s default)                     │ │
│  │ Passive share policy (never announces)                 │ │
│  │ IServerTransport abstraction                           │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ SignalR Hub (transport implementation)                  │ │
│  │ Routes messages between clients and SyncServer         │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## 1. Six.Automerge.TextEdit Library

### 1.1 Project Structure

```
libs/Six.Automerge.TextEdit/
├── Six.Automerge.TextEdit.csproj
├── GlobalUsings.cs
└── src/
    ├── TextDocument.cs           # Main API surface
    ├── TextDocumentOptions.cs    # Configuration
    ├── TextDiff.cs               # Diff/splice algorithm
    ├── CursorTracker.cs          # Cursor save/restore
    ├── ChangeDebouncer.cs        # Keystroke batching
    ├── Models/
    │   ├── FormattedText.cs      # Rich text model
    │   ├── FormattedSpan.cs      # Text segment with marks
    │   ├── TextMark.cs           # Mark definition
    │   └── TextChangeEventArgs.cs
    └── Extensions/
        └── DocHandleTextExtensions.cs  # DocHandle<T> extensions
```

### 1.2 TextDocument Class

```csharp
namespace Six.Automerge.TextEdit;

/// <summary>
/// High-level text editing abstraction over an Automerge DocHandle.
/// Thread-safe, fully async, and UI-framework-agnostic.
/// </summary>
public sealed class TextDocument : IAsyncDisposable
{
  // Construction
  public TextDocument(DocHandle<object> handle, TextDocumentOptions? options = null);
  public static async Task<TextDocument> CreateAsync(Repo repo, TextDocumentOptions? options = null);
  public static async Task<TextDocument?> OpenAsync(Repo repo, string documentId, TextDocumentOptions? options = null);

  // Properties
  public string DocumentId { get; }
  public string Text { get; }                  // Cached plain text (updated on change)
  public FormattedText FormattedText { get; }  // Cached formatted spans (updated on change)
  public bool IsReady { get; }

  // Text editing — computes diff and applies via DocHandle.ChangeAsync()
  public ValueTask ApplyTextChangeAsync(string newText);

  // Mark operations — applies via DocHandle.ChangeAsync()
  public ValueTask ApplyMarkAsync(string markName, ulong start, ulong end, object value);
  public ValueTask RemoveMarkAsync(string markName, ulong start, ulong end);

  // Cursor management
  public ValueTask<CursorState> SaveCursorAsync(int position);
  public ValueTask<int> RestoreCursorAsync(CursorState cursor);

  // Events
  public event EventHandler<TextChangedEventArgs>? TextChanged;  // Fires for both local and remote changes
  public event EventHandler<FormattedTextChangedEventArgs>? FormattedTextChanged;  // Fires with span updates

  // Lifecycle
  public ValueTask DisposeAsync();
}
```

### 1.3 TextDiff Algorithm

```csharp
namespace Six.Automerge.TextEdit;

/// <summary>
/// Computes minimal text diff between old and new text,
/// producing a splice operation (offset, deleteCount, insertText).
/// </summary>
internal static class TextDiff
{
  /// <summary>
  /// Finds common prefix and suffix to compute a single splice operation.
  /// </summary>
  public static (int Offset, int DeleteCount, string InsertText) ComputeSplice(
    string oldText, string newText);
}
```

This is the cleaned-up version of the current `ApplyTextChange` diff logic, extracted as a pure function.

### 1.4 ChangeDebouncer

```csharp
namespace Six.Automerge.TextEdit;

/// <summary>
/// Batches rapid text changes into fewer Automerge commits.
/// Uses deferred-final pattern: waits for silence before committing.
/// </summary>
internal sealed class ChangeDebouncer : IAsyncDisposable
{
  public ChangeDebouncer(int debounceMs = 50);

  /// <summary>
  /// Queues a text change. If no further changes arrive within debounceMs,
  /// the accumulated change is flushed via the provided callback.
  /// </summary>
  public void QueueChange(string newText, Func<string, ValueTask> flushCallback);

  /// <summary>
  /// Immediately flushes any pending changes.
  /// </summary>
  public ValueTask FlushAsync();
}
```

### 1.5 FormattedText Model

```csharp
namespace Six.Automerge.TextEdit;

/// <summary>
/// Represents text with inline formatting, UI-framework-agnostic.
/// </summary>
public sealed class FormattedText
{
  public IReadOnlyList<FormattedSpan> Spans { get; }
  public string PlainText { get; }
}

public sealed class FormattedSpan
{
  public string Text { get; }
  public int Offset { get; }
  public int Length { get; }
  public IReadOnlyDictionary<string, object> Marks { get; }

  // Convenience accessors
  public bool IsBold => Marks.ContainsKey("bold");
  public bool IsItalic => Marks.ContainsKey("italic");
  public bool IsUnderline => Marks.ContainsKey("underline");
  public string? LinkUrl => Marks.TryGetValue("link", out var v) ? v as string : null;
}

public sealed record TextMark(string Name, object Value, ulong Start, ulong End);
```

### 1.6 Internal Flow

```
User types "Hello" → ApplyTextChangeAsync("Hello")
  │
  ├─ ChangeDebouncer queues "Hello"
  │   (if another keystroke within 50ms, merges)
  │
  ├─ After 50ms silence, flushes:
  │   ├─ TextDiff.ComputeSplice(oldText="", newText="Hello")
  │   │   → (offset=0, delete=0, insert="Hello")
  │   │
  │   ├─ DocHandle.ChangeAsync(doc =>
  │   │   {
  │   │     doc.SpliceText(textId, 0, 0, "Hello");
  │   │   }, "Text edit")
  │   │
  │   ├─ Update cached Text and FormattedText
  │   └─ Fire TextChanged event (isLocal=true)
  │
  └─ DocHandle.HeadsChanged fires
     → Repo schedules debounced save (100ms)
     → DocSynchronizer generates throttled sync message (100ms)
     → Network sends to peers
```

```
Remote change arrives → DocHandle.Changed event (isLocal=false)
  │
  ├─ TextDocument.OnDocHandleChanged()
  │   ├─ await DocHandle.ReadAsync(doc => doc.GetText(textId))
  │   ├─ await DocHandle.ReadAsync(doc => doc.GetSpans(textId))
  │   ├─ Update cached Text and FormattedText
  │   └─ Fire TextChanged + FormattedTextChanged events
  │
  └─ EditorViewModel receives event
     → Updates UI on dispatcher thread
```

---

## 2. Six.Automerge.SyncServer Library

### 2.1 Project Structure

```
libs/Six.Automerge.SyncServer/
├── Six.Automerge.SyncServer.csproj
├── GlobalUsings.cs
└── src/
    ├── SyncServer.cs              # Main API surface
    ├── SyncServerOptions.cs       # Configuration
    ├── IServerTransport.cs        # Transport abstraction
    ├── ServerPeerContext.cs        # Connected peer state
    └── DebouncedPersistence.cs    # Timer-based save
```

### 2.2 SyncServer Class

```csharp
namespace Six.Automerge.SyncServer;

/// <summary>
/// Authoritative Automerge document server.
/// Holds the source-of-truth for all documents and participates
/// in the sync protocol as a peer with passive share policy.
/// </summary>
public sealed class SyncServer : IAsyncDisposable
{
  public SyncServer(SyncServerOptions options);

  // Properties
  public PeerId PeerId { get; }
  public bool IsRunning { get; }

  // Lifecycle
  public Task StartAsync(CancellationToken ct = default);
  public Task StopAsync(CancellationToken ct = default);

  // Document management
  public Task<IReadOnlyList<string>> ListDocumentsAsync();
  public Task<byte[]?> ExportDocumentAsync(string documentId);
  public Task DeleteDocumentAsync(string documentId);

  // Peer management (called by transport layer)
  public Task OnPeerConnectedAsync(string connectionId, PeerId peerId, PeerMetadata? metadata);
  public Task OnPeerDisconnectedAsync(string connectionId, PeerId peerId);
  public Task OnMessageReceivedAsync(PeerId senderId, IRepoMessage message);

  // Send callback (set by transport layer)
  public Func<PeerId, IRepoMessage, Task>? SendToClient { get; set; }
  public Func<string, IRepoMessage, Task>? BroadcastToDocument { get; set; }
}
```

### 2.3 SyncServerOptions

```csharp
public sealed record SyncServerOptions
{
  /// <summary>Storage adapter for persistence.</summary>
  public required IStorageAdapter Storage { get; init; }

  /// <summary>Interval between debounced document saves. Default: 5 seconds.</summary>
  public TimeSpan PersistenceInterval { get; init; } = TimeSpan.FromSeconds(5);

  /// <summary>Sync message debounce rate in ms. Default: 100ms.</summary>
  public int SyncDebounceRate { get; init; } = 100;

  /// <summary>Save debounce rate in ms. Default: 100ms (within Repo).</summary>
  public int SaveDebounceRate { get; init; } = 100;

  /// <summary>Logger factory for diagnostics.</summary>
  public ILoggerFactory? LoggerFactory { get; init; }
}
```

### 2.4 IServerTransport

```csharp
namespace Six.Automerge.SyncServer;

/// <summary>
/// Transport abstraction for the sync server.
/// Implement this for SignalR, gRPC, WebSockets, or Orleans.
/// </summary>
public interface IServerTransport
{
  /// <summary>Send a message to a specific peer.</summary>
  Task SendAsync(PeerId peerId, IRepoMessage message);

  /// <summary>Broadcast a message to all peers subscribed to a document.</summary>
  Task BroadcastAsync(string documentId, IRepoMessage message);

  /// <summary>Fired when a peer connects.</summary>
  event EventHandler<ServerPeerConnectedEventArgs> PeerConnected;

  /// <summary>Fired when a peer disconnects.</summary>
  event EventHandler<ServerPeerDisconnectedEventArgs> PeerDisconnected;

  /// <summary>Fired when a message is received from a peer.</summary>
  event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
}
```

### 2.5 DebouncedPersistence

```csharp
namespace Six.Automerge.SyncServer;

/// <summary>
/// Timer-based persistence that batches document saves.
/// Instead of saving after every change, accumulates dirty documents
/// and flushes on a configurable interval (default 5s).
/// </summary>
internal sealed class DebouncedPersistence : IAsyncDisposable
{
  // Tracks which documents are dirty
  // Timer fires every PersistenceInterval and flushes all dirty docs
  // Individual document saves still use Repo's 100ms save debounce internally
  // This is an ADDITIONAL layer: Repo debounce (100ms) + Server timer (5s)
}
```

### 2.6 Server Architecture

```
Client connects → Transport.PeerConnected → SyncServer.OnPeerConnectedAsync()
  └─ Creates adapter peer, Repo begins sync

Client sends sync message → Transport.MessageReceived → SyncServer.OnMessageReceivedAsync()
  └─ Routes to internal network adapter
    └─ Repo processes via CollectionSynchronizer → DocSynchronizer
      └─ ReceiveSyncMessage: document updated
      └─ SendSyncMessage: response generated
        └─ Internal adapter.Send() → SyncServer.SendToClient() → Transport.SendAsync()

Document changes → Repo HeadsChanged → Repo ScheduleSave (100ms debounce)
  └─ DebouncedPersistence marks document dirty
    └─ Timer fires every 5s → flush all dirty docs to storage
```

The Repo internal to SyncServer uses:
- `ShareConfig = { Announce = () => false, Access = () => true }` (passive: never announces, always allows access when requested)
- An in-process `INetworkAdapter` implementation that bridges to IServerTransport

---

## 3. Sync Throttling in DocSynchronizer

### 3.1 Changes to DocSynchronizer

Add a deferred-final throttle to `NotifyLocalChange()`:

```csharp
// Current (no throttle):
public async Task NotifyLocalChange()
{
  foreach(var peer in _activePeers)
    await SendSyncMessage(peer, deferredEvents);
}

// New (throttled):
public async Task NotifyLocalChange()
{
  _syncThrottle.Schedule();  // Resets timer, fires after delay
}

// Timer callback:
async Task FlushSyncMessages()
{
  foreach(var peer in _activePeers)
    await SendSyncMessage(peer, deferredEvents);
}
```

### 3.2 Changes to RepoConfig

```csharp
public sealed record RepoConfig
{
  // Existing
  public int SaveDebounceRate { get; init; } = 100;

  // New
  public int SyncDebounceRate { get; init; } = 100;
}
```

### 3.3 Throttle Behavior

Matches the TS reference implementation:
1. First change arrives → start 100ms timer
2. More changes arrive within 100ms → reset timer
3. 100ms of silence → fire sync messages to all peers
4. Guarantees final state is always sent

---

## 4. Rich Text Rendering

### 4.1 RichTextEditor Control

A custom Avalonia control that provides editable rich text using a dual-layer approach:

```
┌──────────────────────────────────────────┐
│ RichTextEditor (UserControl)              │
│                                           │
│ ┌───────────────────────────────────────┐ │
│ │ ScrollViewer                          │ │
│ │ ┌─────────────────────────────────┐   │ │
│ │ │ Panel (overlay container)       │   │ │
│ │ │                                 │   │ │
│ │ │ Layer 1: TextBlock (display)    │   │ │
│ │ │  Contains Runs/Spans with       │   │ │
│ │ │  Bold/Italic/Underline styling  │   │ │
│ │ │                                 │   │ │
│ │ │ Layer 2: TextBox (input)        │   │ │
│ │ │  Transparent text, handles      │   │ │
│ │ │  keyboard input, selection,     │   │ │
│ │ │  clipboard                      │   │ │
│ │ └─────────────────────────────────┘   │ │
│ └───────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

**Approach:**
1. **Input layer** — Standard `TextBox` with transparent foreground, handles all keyboard/mouse/clipboard events.
2. **Display layer** — `TextBlock` with `InlineCollection` containing `Run` elements styled with `FontWeight`, `FontStyle`, `TextDecorations` based on `FormattedText.Spans`.
3. **Sync** — When `FormattedText` changes (local or remote), rebuild the `InlineCollection`. Caret position from TextBox maps to text offset for mark operations.

### 4.2 Rendering FormattedSpan → Avalonia Inlines

```csharp
// Pseudocode for rendering
foreach(var span in formattedText.Spans)
{
  var run = new Run(span.Text);

  if(span.IsBold)
    run.FontWeight = FontWeight.Bold;
  if(span.IsItalic)
    run.FontStyle = FontStyle.Italic;
  if(span.IsUnderline)
    run.TextDecorations = TextDecorations.Underline;

  textBlock.Inlines.Add(run);
}
```

---

## 5. Refactored EditorViewModel

### 5.1 Simplified Architecture

```csharp
public class EditorViewModel : ViewModelBase, IAsyncDisposable
{
  TextDocument? _textDocument;

  // Properties bound to UI
  public string DocumentText { get; set; }         // Plain text for TextBox
  public FormattedText? FormattedText { get; set; } // Formatted spans for display
  public string SyncStatus { get; set; }
  public int CursorPosition { get; set; }
  public int SelectionLength { get; set; }
  public bool IsBold { get; set; }
  public bool IsItalic { get; set; }
  public bool IsUnderline { get; set; }

  // Text editing — delegates entirely to TextDocument
  public async Task OnTextChangedAsync(string newText)
  {
    if(_textDocument is not null && !_isRemoteUpdate)
      await _textDocument.ApplyTextChangeAsync(newText);
  }

  // Mark operations — delegates to TextDocument
  public async Task ToggleBoldAsync()
  {
    if(_textDocument is null || _selectionLength <= 0) return;
    if(_isBold)
      await _textDocument.RemoveMarkAsync("bold", start, end);
    else
      await _textDocument.ApplyMarkAsync("bold", start, end, true);
  }

  // Remote changes — receives from TextDocument events
  void OnTextDocumentChanged(object? sender, TextChangedEventArgs e)
  {
    _dispatcher(() =>
    {
      _isRemoteUpdate = true;
      DocumentText = e.Text;
      FormattedText = e.FormattedText;
      _isRemoteUpdate = false;
    });
  }
}
```

### 5.2 Key Simplifications

- No `AutomergeService` — replaced by `TextDocument`
- No `WithDocumentLock()` — `TextDocument` handles locking internally
- No `ApplyTextChange()` diff logic — moved to `TextDocument.ApplyTextChangeAsync()`
- No `SaveCursorBeforeEdit` / `RestoreCursorAfterEdit` — moved to `TextDocument.SaveCursorAsync()` / `RestoreCursorAsync()`
- No direct `((IDocHandle)handle).DocumentLock` access
- All async, no `.Wait()` or `.GetAwaiter().GetResult()`

---

## 6. Refactored Server.Api

### 6.1 Simplified Server

```csharp
// Program.cs / Startup
var syncServer = new SyncServer(new SyncServerOptions
{
  Storage = new FileSystemStorageAdapter(storagePath),
  PersistenceInterval = TimeSpan.FromSeconds(5),
  LoggerFactory = loggerFactory
});

// Wire to SignalR hub
builder.Services.AddSingleton(syncServer);
builder.Services.AddSignalR();

app.MapHub<DocumentsHub>("/hubs/documents");

await syncServer.StartAsync();
```

### 6.2 Hub as Transport

The `DocumentsHub` acts as the SignalR transport implementation:
- `Handshake()` → `syncServer.OnPeerConnectedAsync()`
- `SendMessage()` → `syncServer.OnMessageReceivedAsync()`
- `OnDisconnectedAsync()` → `syncServer.OnPeerDisconnectedAsync()`
- `syncServer.SendToClient` → `Clients.Client(connectionId).OnReceiveMessage()`
- `syncServer.BroadcastToDocument` → `Clients.OthersInGroup(docId).OnReceiveMessage()`

---

## 7. Data Flow Diagram

```
User types "Hi" in Client A
  │
  ▼
TextBox.Text changes → EditorViewModel.OnTextChangedAsync("Hi")
  │
  ▼
TextDocument.ApplyTextChangeAsync("Hi")
  ├─ ChangeDebouncer queues (50ms)
  ├─ After silence: TextDiff.ComputeSplice("", "Hi") → splice(0, 0, "Hi")
  ├─ DocHandle.ChangeAsync(doc => doc.SpliceText(textId, 0, 0, "Hi"))
  ├─ Updates cached Text="Hi", FormattedText=[{text:"Hi", marks:{}}]
  └─ Fires TextChanged event → VM updates FormattedText property → UI renders

DocHandle.HeadsChanged fires
  ├─ Repo.ScheduleSave (100ms debounce)
  └─ DocSynchronizer.NotifyLocalChange()
      ├─ SyncThrottle (100ms debounce)
      └─ After silence: GenerateSyncMessage → NetworkAdapter.Send()

SignalR sends to Server
  ├─ SyncServer.OnMessageReceivedAsync()
  │   └─ Server Repo processes, updates server document
  │   └─ Server generates response → sends back to Client A (and others)
  │
  └─ Hub broadcasts to Client B, C, D in document group

Client B receives sync message
  ├─ SignalRNetworkAdapter.OnReceiveMessage()
  ├─ Repo.OnMessageReceived → DocSynchronizer.ReceiveSyncMessage()
  ├─ DocHandle.NotifyRemoteChangeAsync()
  └─ DocHandle.Changed event (isLocal=false)
      └─ TextDocument.OnDocHandleChanged()
          ├─ Reads updated text and spans from document
          ├─ Updates cached Text and FormattedText
          └─ Fires TextChanged + FormattedTextChanged
              └─ EditorViewModel receives → dispatches to UI
                  └─ RichTextEditor renders spans with bold/italic
```
