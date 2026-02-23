# Design: CollaborativeEditor Crash & Sync Stability Fixes

## Overview

This design addresses six categories of crash vectors in the collaborative editing stack. Changes span three layers: sync infrastructure, FFI bindings, and UI ViewModel. The approach is surgical — fix identified bugs, add diagnostic logging, and verify with E2E testing via Aspire.

## Architecture Context

```
┌──────────────────────────────────────────────────────────────┐
│  EditorViewModel (UI thread)                                  │
│  - _isRemoteUpdate flag guards against recursive splices      │
│  - DocumentText setter → ApplyTextChange() → SpliceText       │
│  - OnDocHandleChanged → RefreshFromDocument()                 │
├──────────────────────────────────────────────────────────────┤
│  DocHandle<T> (lock: _documentLock SemaphoreSlim(1,1))        │
│  - ChangeAsync() holds lock during mutations                  │
│  - NotifyRemoteChange() BLOCKS on lock ← FIX NEEDED          │
│  - ReadAsync() holds lock during reads                        │
├──────────────────────────────────────────────────────────────┤
│  DocSynchronizer (per-document sync orchestration)            │
│  - ReceiveSyncMessage() acquires doc lock (nullable!) ← FIX  │
│  - SendSyncMessage() acquires doc lock twice ← FIX           │
│  - _updateDocument() called outside lock ← FIX               │
├──────────────────────────────────────────────────────────────┤
│  CollectionSynchronizer (manages DocSynchronizers per peer)   │
│  - AddDocument() / OnPeerConnected() await inside lock        │
├──────────────────────────────────────────────────────────────┤
│  AutomergeDocument (FFI wrapper, NOT thread-safe)             │
│  - SpliceText, Put*, ReceiveSyncMessage, GenerateSyncMessage  │
│  - Missing ThrowIfDisposed() on 23+ methods ← FIX            │
├──────────────────────────────────────────────────────────────┤
│  Rust FFI (libsix_automerge.so/.dll — single-threaded)        │
└──────────────────────────────────────────────────────────────┘
```

## Design Decisions

### D1: DocSynchronizer Lock Strategy

**Problem:** `_getDocumentLock()` returns `SemaphoreSlim?`. When null, FFI calls run unprotected.

**Fix:** Change the lock acquisition to throw if null. A null lock means the document handle is not registered — syncing a document without a registered handle is a bug, not an expected state.

```csharp
// Before
var docLock = _getDocumentLock();
docLock?.Wait();
try { /* FFI call */ }
finally { docLock?.Release(); }

// After
var docLock = _getDocumentLock()
  ?? throw new InvalidOperationException($"No document lock for {_documentId}");
docLock.Wait();
try { /* FFI call */ }
finally { docLock.Release(); }
```

**Alternative considered:** Fall back to a local lock if null. Rejected because it masks a real bug (sync without registration).

### D2: Lock Scope in ReceiveSyncMessage

**Problem:** Lock released before `_updateDocument(doc)` callback.

**Fix:** Move `_updateDocument(doc)` inside the lock scope. The callback notifies DocHandle, which needs consistent document state.

```csharp
docLock.Wait();
try
{
  doc.ReceiveSyncMessage(syncState, message.Data);
  _updateDocument(doc);  // Now inside lock
}
finally
{
  docLock.Release();
}
```

### D3: Lock Scope in SendSyncMessage

**Problem:** Lock acquired twice — once for `GenerateSyncMessage`, once for `Save`. Document can change between.

**Fix:** Single lock acquisition covering both operations.

```csharp
docLock.Wait();
try
{
  messageData = doc.GenerateSyncMessage(peerSyncState);
  if(peerStatus != PeerDocumentStatus.Has)
  {
    snapshotData = doc.Save();
  }
}
finally
{
  docLock.Release();
}
```

### D4: DocHandle.NotifyRemoteChange — Async Pattern

**Problem:** Synchronous `_documentLock.Wait()` blocks thread pool under heavy sync.

**Fix:** Convert to async with `WaitAsync()`. Since the caller (`_updateDocument` in Repo) already wraps this in a task, making it async is natural. Update the delegate signature to return `Task` and update `DocSynchronizer._updateDocument` to be `Func<AutomergeDocument, Task>`.

**Alternative considered:** Keep synchronous but add timeout. Rejected because it doesn't fix the deadlock; it just makes it time out.

### D5: AutomergeDocument Disposal Checks

**Problem:** 23+ public methods missing `ThrowIfDisposed()`.

**Fix:** Add `ThrowIfDisposed()` as the first line of every public method that accesses `_doc`. The pattern already exists in some methods (`PutString`, `GetString`, etc.) — standardize it.

Methods to fix (grouped by category):
- **Map writes:** `PutBoolean`, `PutInt64`, `PutUInt64`, `PutDouble`, `PutBytes`, `PutNull`, `PutCounter`, `PutTimestamp`
- **List writes:** `ListInsertString`, `ListInsertBoolean`, `ListInsertInt64`, `ListInsertUInt64`, `ListInsertDouble`, `ListInsertBytes`, `ListInsertNull`, `ListRemove`, `ListPutString`, `ListPutBoolean`, `ListPutInt64`, `ListPutUInt64`, `ListPutDouble`, `ListPutBytes`, `ListPutNull`, `ListSplice`
- **Text/marks:** `SpliceText`, `Mark` (all overloads)
- **Other mutations:** `Delete`, `Increment` (both overloads), `ApplyChange`, `ApplyChanges`
- **Sync:** `ReceiveSyncMessage`, `GenerateSyncMessage`

### D6: AutomergeSyncState Disposal Checks

**Fix:** Add `_disposed` bool field and `ThrowIfDisposed()` to `AutomergeSyncState`. Check in: `Reset()`, `SharedHeads`, `Encode()`, `Decode()`.

### D7: EditorViewModel._isRemoteUpdate Thread Safety

**Problem:** Flag set/unset without synchronization. Rapid remote changes can corrupt it.

**Fix:** Use `Interlocked.Exchange` pattern:

```csharp
// In RefreshFromDocument:
if(Interlocked.CompareExchange(ref _isRemoteUpdate, 1, 0) == 0)
{
  try
  {
    _documentText = text;
    this.RaisePropertyChanged(nameof(DocumentText));
  }
  finally
  {
    Interlocked.Exchange(ref _isRemoteUpdate, 0);
  }
}
```

Change `_isRemoteUpdate` from `bool` to `int` for `Interlocked` compatibility. Update the `DocumentText` setter check accordingly.

### D8: Repo.Find() Lock Safety

**Problem:** `_handleLock` can be double-released.

**Fix:** Use a `bool lockHeld` tracking pattern:

```csharp
bool lockHeld = true;
await _handleLock.WaitAsync().ConfigureAwait(false);
try
{
  if(_findInProgress.TryGetValue(documentId, out findTask))
  {
    _handleLock.Release();
    lockHeld = false;
    var result = await findTask.ConfigureAwait(false);
    return (DocHandle<T>)result;
  }
  // ... rest of method
}
finally
{
  if(lockHeld) _handleLock.Release();
}
```

### D9: Logging Strategy

All new logging uses `ILogger<T>` with structured logging via `LoggerMessage.Define` or source generators for zero-allocation logging in hot paths.

**Log categories and levels:**

| Operation | Level | Includes |
|-----------|-------|----------|
| Lock acquire/release | `Debug` | DocumentId, caller method |
| Sync message send/receive | `Debug` | DocumentId, PeerId, message size |
| Document state transitions | `Information` | DocumentId, old state → new state |
| FFI call errors | `Error` | DocumentId, exception details |
| Silent catch blocks (now logged) | `Warning` | Context, exception |
| Fire-and-forget failures | `Error` | Operation name, exception |

**Correlation:** All log messages include `DocumentId` for filtering in Aspire. Sync operations also include `PeerId`.

### D10: Fire-and-Forget Task Safety

Replace bare `Task.Run(() => ...)` patterns with logged wrapper:

```csharp
_ = Task.Run(async () =>
{
  try { await operation().ConfigureAwait(false); }
  catch(Exception ex)
  {
    _logger.LogError(ex, "Background operation {Operation} failed for {DocumentId}", opName, docId);
  }
});
```

This preserves the fire-and-forget behavior (intentional — these operations should not block the caller) but makes failures visible.

## Files Modified

| File | Changes |
|------|---------|
| `libs/Six.Automerge/src/AutomergeDocument.cs` | Add `ThrowIfDisposed()` to 23+ methods |
| `libs/Six.Automerge/src/AutomergeSyncState.cs` | Add `_disposed` flag and checks |
| `libs/Six.Automerge.Repo/src/DocHandle.cs` | Make `NotifyRemoteChange` async; add logging |
| `libs/Six.Automerge.Repo/src/Synchronizer/DocSynchronizer.cs` | Fix lock scopes; add logging; make `_updateDocument` async |
| `libs/Six.Automerge.Repo/src/Synchronizer/CollectionSynchronizer.cs` | Add logging |
| `libs/Six.Automerge.Repo/src/Repo.cs` | Fix `Find()` lock; add logging to fire-and-forget tasks |
| `libs/Six.Automerge.Repo/src/IDocHandle.cs` | Update `NotifyRemoteChange` signature if needed |
| `apps/DemoApp.CollaborativeEditor/src/ViewModels/EditorViewModel.cs` | Atomicize `_isRemoteUpdate`; add logging |
| `tests/Six.Automerge.Repo.Tests.Unit/src/Synchronizer/DocSynchronizerTests.cs` | Update tests for new lock behavior |
| `tests/Six.Automerge.Repo.Tests.Unit/src/Synchronizer/CollectionSynchronizerTests.cs` | Update tests for logging |

## E2E Verification Plan

1. Start full stack via `dotnet run --project apps/Infra.AppHost`
2. Wait for Aspire dashboard and all 4 CollaborativeEditor replicas to be healthy
3. Use `six-avalonia-mcp` to:
   a. Create a new document in Client 1
   b. Copy document ID and join from Client 2 and Client 3
   c. In Client 1: type rapidly — paragraphs of text with returns, backspaces, selections, deletions
   d. Observe Client 2 and Client 3 continue receiving updates
   e. Check Aspire traces for errors, warnings, and deadlock indicators
4. Reverse roles — type in Client 2, verify Client 1 and Client 3 stay synced
5. Run for 2+ minutes of continuous heavy editing without any client crashing
