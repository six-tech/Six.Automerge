# Design: Document Sync Issues Fix

## Overview

Three interconnected bugs must be fixed across the storage layer, sync server, text editing layer, and SignalR network adapter. The fixes are ordered from lowest layer (storage) to highest (UI) to ensure each fix builds on a stable foundation.

## Bug 1: FileSystemStorageAdapter Empty Prefix

### Root Cause

`SyncServer.ListDocumentsAsync()` creates `new StorageKey(Array.Empty<string>())` and passes it to `GetChunkInfo()`. The `InMemoryStorageAdapter` handles this correctly (empty prefix matches all keys). But `FileSystemStorageAdapter.GetChunkInfo()` calls `GetFilePath(keyPrefix)` which throws `ArgumentException("StorageKey must have at least one component")` at line 342.

### Fix Design

**File: `libs/Six.Automerge.Repo.Storage.FileSystem/src/FileSystemStorageAdapter.cs`**

Modify `GetChunkInfo()` to handle empty prefix as a special case that scans the entire base directory:

```
GetChunkInfo(StorageKey keyPrefix):
  if keyPrefix.Components.Count == 0:
    // Scan ALL shard directories (2-char dirs under _baseDirectory)
    foreach shardDir in Directory.GetDirectories(_baseDirectory):
      foreach file in WalkDirectory(shardDir):
        components = ParseFilePathToKey(relativePath)
        results.Add(new ChunkInfo(new StorageKey(components), fileSize))
    return results
  else:
    // Existing logic: GetFilePath(keyPrefix) + WalkDirectory
```

Similarly update `LoadRange()` (inherited from `StorageAdapterBase` or overridden) if it also goes through `GetFilePath`.

**Do NOT modify `GetFilePath()`** — keep the validation there. The empty-prefix case is handled BEFORE calling `GetFilePath`.

### Affected Files
- `libs/Six.Automerge.Repo.Storage.FileSystem/src/FileSystemStorageAdapter.cs` — `GetChunkInfo()` method
- `tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit/` — add test for empty prefix

## Bug 2: Document Discovery Across Clients

### Root Cause (Two-Part)

**Part A:** `SyncServer.ListDocumentsAsync()` only returns documents that have been persisted to storage. After a client creates a document and syncs it, the server's `DebouncedPersistence` may not have flushed yet, so storage is empty.

**Part B:** There is no proactive notification when the server learns about a new document. Other clients must manually refresh to call `ListDocuments`, which fails anyway due to Bug 1.

### Fix Design

**Part A — In-Memory Document Tracking in SyncServer:**

**File: `libs/Six.Automerge.SyncServer/src/SyncServer.cs`**

Add a `ConcurrentDictionary<string, byte> _knownDocumentIds` field. Update:

```csharp
// In OnMessageReceivedAsync:
if(message.DocumentId is { } documentId)
{
  _knownDocumentIds.TryAdd(documentId.Value, 0);  // Track new document
  _persistence.MarkDirty(documentId.Value);
}

// In ListDocumentsAsync:
// Combine in-memory tracked IDs + storage IDs
var documentIds = new HashSet<string>(_knownDocumentIds.Keys);
// Also scan storage for IDs (in case server restarted and loaded from storage)
var chunks = await _options.Storage.GetChunkInfo(emptyPrefix);
foreach(var chunk in chunks)
{
  if(chunk.Key.Components is { Count: > 0 })
    documentIds.Add(chunk.Key.Components[0]);
}
return documentIds.ToList();
```

Also populate `_knownDocumentIds` in the `OnDocumentLoaded` event handler so documents restored from storage are also tracked.

**Part B — New Document Notification via SignalR:**

**File: `apps/Server.Api/src/Hubs/DocumentsHub.cs`** and **`apps/Server.Api/src/Hubs/SignalRServerTransport.cs`**

Add a new event to `SyncServer` or `IServerTransport` that fires when a new document ID is first seen. The `DocumentsHub` subscribes to this event and broadcasts to all clients:

```
// New hub client interface method:
IDocumentsHubClient.OnDocumentAvailable(string documentId)

// In DocumentsHub.SendMessage():
// After syncServer.OnMessageReceivedAsync, check if this is a new document
// and broadcast to all clients
```

**File: `libs/Six.Automerge.Repo.Network.SignalR.Abstractions/`**

Add `OnDocumentAvailable(string documentId)` to `IDocumentsHubClient` interface.

**File: `libs/Six.Automerge.Repo.Network.SignalR.Client/src/SignalRNetworkAdapter.cs`**

Register callback for `OnDocumentAvailable` — raise a new event that the `RepoService` can subscribe to for auto-refresh.

**File: `apps/DemoApp.Shared/src/RepoService.cs`**

Subscribe to the new document available event and re-fetch the document list, notifying the UI.

### Affected Files
- `libs/Six.Automerge.SyncServer/src/SyncServer.cs` — in-memory tracking + event
- `libs/Six.Automerge.Repo.Network.SignalR.Abstractions/` — `IDocumentsHubClient` interface addition
- `apps/Server.Api/src/Hubs/DocumentsHub.cs` — subscribe to new-doc event, broadcast
- `libs/Six.Automerge.Repo.Network.SignalR.Client/src/SignalRNetworkAdapter.cs` — register callback
- `apps/DemoApp.Shared/src/RepoService.cs` — subscribe and refresh
- `apps/DemoApp.Shared/src/Controls/DocumentSessionDialogViewModel.cs` — auto-refresh on notification

## Bug 3: Client Crash During Typing (Rust Panic)

### Root Cause Analysis

The crash is caused by a race condition between local text edits and the server's echo-back sync messages. The precise sequence:

1. User types character → `EditorViewModel.DocumentText` setter fires `_textDocument.ApplyTextChangeAsync(value)` (fire-and-forget, line 63)
2. `ApplyTextChangeAsync` schedules via `ChangeDebouncer`, which eventually calls `FlushTextChangeAsync`
3. `FlushTextChangeAsync` sets `_isLocalChange = true`, commits via `ChangeAsync`, then sets `_isLocalChange = false` (line 309)
4. The committed change triggers `DocHandle.Changed` event → repo sync → server receives and responds
5. Server response arrives at client → `DocHandle.Changed` fires again
6. Since `_isLocalChange` is already `false` (step 3), `OnDocHandleChanged` treats it as a remote change
7. `HandleRemoteChangeAsync` (fire-and-forget) calls `RefreshFromDocumentAsync` to re-read `_cachedText`
8. Meanwhile, user types more characters → new `FlushTextChangeAsync` reads stale `_cachedText`
9. `TextDiff.ComputeSplice` computes wrong offsets → wrong `SpliceText` call → Rust panic

The garbled text (Some → Soe → Sod → Somd) is direct evidence of incorrect splice positions.

### Fix Design

**Strategy: Replace flag-based exclusion with a serialized async operation queue.**

**File: `libs/Six.Automerge.TextEdit/src/TextDocument.cs`**

Replace the `_isLocalChange` boolean flag with a `Channel<TextOperation>` that serializes all operations:

```csharp
sealed class TextDocument : IDisposable
{
  // REMOVE: bool _isLocalChange;
  // ADD:
  readonly Channel<TextOperation> _operationQueue = Channel.CreateUnbounded<TextOperation>();
  Task? _processingTask;

  // TextOperation discriminated union:
  abstract record TextOperation;
  record LocalTextChange(string NewText) : TextOperation;
  record RemoteRefresh : TextOperation;

  internal async Task InitializeAsync(...)
  {
    await RefreshFromDocumentAsync(cancellationToken);
    _handle.Changed += OnDocHandleChanged;
    _processingTask = ProcessOperationQueueAsync();  // Start queue processor
  }

  public async Task ApplyTextChangeAsync(string newText, ...)
  {
    EnsureNotDisposed();
    // Write directly to queue instead of debouncer
    _operationQueue.Writer.TryWrite(new LocalTextChange(newText));
  }

  void OnDocHandleChanged(object? sender, DocHandleChangeEventArgs<object> e)
  {
    if(_isDisposed) return;
    _operationQueue.Writer.TryWrite(new RemoteRefresh());
  }

  async Task ProcessOperationQueueAsync()
  {
    // Internal debounce state for local changes
    string? pendingLocalText = null;
    var debounceDelay = TimeSpan.FromMilliseconds(_options.ChangeDebounceMs);

    await foreach(var op in _operationQueue.Reader.ReadAllAsync())
    {
      switch(op)
      {
        case LocalTextChange local:
          pendingLocalText = local.NewText;
          // Drain additional pending local changes (debounce coalescing)
          while(_operationQueue.Reader.TryRead(out var next))
          {
            if(next is LocalTextChange moreLocal)
              pendingLocalText = moreLocal.NewText;
            else if(next is RemoteRefresh)
              break; // Process remote refresh after flushing local
          }
          // Flush the coalesced local change
          await FlushLocalChangeAsync(pendingLocalText);
          pendingLocalText = null;
          break;

        case RemoteRefresh:
          // Only refresh if no pending local changes
          if(pendingLocalText is null)
          {
            await RefreshFromDocumentAsync(CancellationToken.None);
            TextChanged?.Invoke(this, new TextChangedEventArgs(_cachedText, false));
            FormattedTextChanged?.Invoke(this, new FormattedTextChangedEventArgs(_cachedFormattedText, false));
          }
          // If there ARE pending local changes, the remote refresh will happen
          // naturally after the next local flush (FlushLocalChangeAsync calls RefreshFromDocumentAsync)
          break;
      }
    }
  }

  async Task FlushLocalChangeAsync(string newText)
  {
    string currentText = _cachedText;
    var splice = TextDiff.ComputeSplice(currentText, newText);
    if(!splice.HasChanges) return;

    await _handle.ChangeAsync(doc =>
      doc.SpliceText(_textObjectId, (ulong)splice.Offset, splice.DeleteCount, splice.InsertText));

    await RefreshFromDocumentAsync(CancellationToken.None);
    TextChanged?.Invoke(this, new TextChangedEventArgs(_cachedText, true));
  }
}
```

**Key properties of this design:**
- **Serialization**: All operations go through a single queue, processed one at a time
- **No concurrent splices**: Local and remote operations can never overlap
- **Debounce coalescing**: Multiple rapid keystrokes are coalesced by reading all available `LocalTextChange` items before processing
- **Echo-back immunity**: When an echo-back arrives as `RemoteRefresh`, if there are pending local changes it's skipped (the local flush will refresh anyway)
- **No `_isLocalChange` flag**: Eliminated entirely — the queue's sequential processing provides the exclusion guarantee

**File: `apps/DemoApp.CollaborativeEditor/src/ViewModels/EditorViewModel.cs`**

The `DocumentText` setter remains largely the same (fire-and-forget into `ApplyTextChangeAsync`), but now `ApplyTextChangeAsync` writes to a serialized queue instead of a debouncer.

Remove the `Console.Error.WriteLine` debug output from the `FormattedText` setter (line 87).

### Affected Files
- `libs/Six.Automerge.TextEdit/src/TextDocument.cs` — major refactor of change processing
- `libs/Six.Automerge.TextEdit/src/ChangeDebouncer.cs` — may be removed or simplified (debouncing moves into the queue processor)
- `apps/DemoApp.CollaborativeEditor/src/ViewModels/EditorViewModel.cs` — remove debug output
- `tests/Six.Automerge.TextEdit.Tests.Unit/` — update tests for new queue-based behavior

## Data Flow After Fix

```
User types "abc" rapidly:
  EditorViewModel.DocumentText = "a"
    → _textDocument.ApplyTextChangeAsync("a")
    → Channel writes LocalTextChange("a")
  EditorViewModel.DocumentText = "ab"
    → Channel writes LocalTextChange("ab")
  EditorViewModel.DocumentText = "abc"
    → Channel writes LocalTextChange("abc")

Queue processor (single thread):
  1. Reads LocalTextChange("a"), drains → finds LocalTextChange("ab"), LocalTextChange("abc")
  2. Coalesces to "abc"
  3. FlushLocalChangeAsync("abc"):
     - ComputeSplice("", "abc") → offset=0, insert="abc"
     - ChangeAsync(SpliceText) → committed to Automerge
     - RefreshFromDocumentAsync → _cachedText = "abc"
     - TextChanged (isLocal=true)
  4. Server echo arrives → Channel writes RemoteRefresh
  5. Reads RemoteRefresh → no pending local → RefreshFromDocumentAsync
     - _cachedText already "abc" → no-op refresh
     - TextChanged (isLocal=false) — but text is same, UI doesn't visually change
```

## Test Strategy

### Existing Tests (must pass)
- All 1,083 tests across all projects

### New Tests
1. **FileSystemStorageAdapter**: `GetChunkInfo_EmptyPrefix_ReturnsAllChunks` / `LoadRange_EmptyPrefix_ReturnsAllChunks`
2. **SyncServer**: `ListDocumentsAsync_ReturnsInMemoryTrackedDocuments` / `ListDocumentsAsync_ReturnsBothInMemoryAndStorageDocuments`
3. **TextDocument**: `ApplyTextChangeAsync_RapidChanges_CoalescesAndAppliesCorrectly` / `RemoteRefresh_DuringPendingLocalChange_IsDeferred`
4. **Integration**: Multi-peer document create → discover → edit scenario (if feasible within existing test infrastructure)
