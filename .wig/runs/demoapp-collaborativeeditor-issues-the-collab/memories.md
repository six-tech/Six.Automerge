# 2026-02-21 19:54:05 UTC | Spec: demoapp-collaborativeeditor-issues-the-collab | Task: sprint-1-task-5 | Sprint: AutomergeDocument & AutomergeSyncState Disposal Safety

**Description**: Run `dotnet build` from repo root to verify full build succeeds with no warnings

AutomergeDocument.cs had 40 public methods missing ThrowIfDisposed() (not just 23+ as estimated in design.md). Beyond the expected map writes, list inserts/puts/remove, text splice, and sync methods, additional missing checks were found in: PutObject, ListLength, GetKeys, HydrateJson, GetLastLocalChange, GetHeads, GetChanges, Merge, ListPutObject, ListInsertObject. AutomergeSyncState.cs also needed disposal safety added — it had no _disposed field, no ThrowIfDisposed method, and Dispose() wasn't idempotent. The internal Context property was also guarded since higher layers use it. The Infra.AppHost project has a pre-existing build error (DistributedApplication not found) unrelated to library code.

# 2026-02-21 20:19:13 UTC | Spec: demoapp-collaborativeeditor-issues-the-collab | Task: sprint-2-task-5 | Sprint: DocSynchronizer Lock & Thread Safety Fixes

**Description**: Update `tests/Six.Automerge.Repo.Tests.Unit/src/Synchronizer/DocSynchronizerTests.cs` — fix any tests broken by the non-nullable lock change (tests may pass null locks that now need to be real `SemaphoreSlim` instances). Run the test project and verify all tests pass.

DocSynchronizer now requires ILogger<DocSynchronizer> as 6th constructor parameter. CollectionSynchronizer accepts optional ILoggerFactory (defaults to NullLoggerFactory.Instance). Repo.GetDocumentLock now returns fallback SemaphoreSlim for documents not yet in handle cache, instead of null. All test constructors need real SemaphoreSlim instances and NullLogger<DocSynchronizer>.Instance. Microsoft.Extensions.Logging.Abstractions 10.0.3 added to Six.Automerge.Repo.csproj.

# 2026-02-21 20:37:51 UTC | Spec: demoapp-collaborativeeditor-issues-the-collab | Task: sprint-3-task-6 | Sprint: DocHandle Async Fix & Repo Lock Safety

**Description**: Run `dotnet test tests/Six.Automerge.Repo.Tests.Unit` to verify all 263 Repo tests still pass

## Sprint 3 Learnings (2026-02-21)

### NotifyRemoteChange Deadlock Fix
- The deadlock chain was: `DocSynchronizer` holds `docLock` → calls `_updateDocument(doc)` → `Repo.UpdateDocument` → `handle.NotifyRemoteChange(document)` → tries to acquire same `_documentLock` = DEADLOCK
- Fix: Made `NotifyRemoteChange` async, moved `_updateDocument(doc)` OUTSIDE the docLock scope in `ReceiveSyncMessage`, changed delegate chain to `Func<AutomergeDocument, Task>` throughout DocSynchronizer → CollectionSynchronizer → Repo
- The delegate type change propagated: `Action<AutomergeDocument>` → `Func<AutomergeDocument, Task>` in DocSynchronizer, `Action<string, AutomergeDocument>` → `Func<string, AutomergeDocument, Task>` in CollectionSynchronizer

### DocHandle Generic Logging
- `DocHandle<T>` is a generic class, so `[LoggerMessage]` source generators don't work (they require partial non-generic classes)
- Used `ILogger` extension methods directly instead
- Logger created with category string: `$"Six.Automerge.Repository.DocHandle<{typeof(T).Name}>"`
- Constructor takes optional `ILogger? logger = null`, defaults to `NullLogger.Instance`

### Find<T>() Double-Release
- The old code used `if(_handleLock.CurrentCount == 0)` as a heuristic to avoid double-release — this is **racy**
- Fixed with `bool lockHeld` tracking variable set to `false` when released early

### Test Update Pattern
- When changing `Action<X>` to `Func<X, Task>` delegates, tests need helper: `static Func<X, Task> AsyncUpdate(Action<X> a) => x => { a(x); return Task.CompletedTask; }`

# 2026-02-21 20:47:12 UTC | Spec: demoapp-collaborativeeditor-issues-the-collab | Task: sprint-4-task-6 | Sprint: EditorViewModel Thread Safety & Error Visibility

**Description**: Run `dotnet build` from repo root — verify zero warnings, zero errors across the full solution

Sprint 4 changes: EditorViewModel._isRemoteUpdate changed from bool to int for Interlocked/Volatile thread safety. EditorViewModel already had ILogger from constructor — added Debug logs in ApplyTextChange (insert/delete counts) and RefreshFromDocument (remote update char count), Warning logs in SaveCursorBeforeEdit and RestoreCursorAfterEdit catch blocks. EditorWindow got ILogger resolved from App.Services in constructor — all catch blocks now log at Warning, all create/join/resume paths log at Information on success and Warning on failure. CollectionSynchronizer got dedicated ILogger<CollectionSynchronizer> created from existing _loggerFactory — AddDocument, OnPeerConnected, OnPeerDisconnected log at Information.

# 2026-02-21 23:30:56 UTC | Spec: demoapp-collaborativeeditor-issues-the-collab | Task: sprint-5-task-5 | Sprint: E2E Integration Testing via Aspire

**Description**: In Client 1: type a substantial amount of text via `six-avalonia-mcp` — multiple paragraphs with newlines (`\n`), backspaces (`\b`), and mixed content. Type at least 200+ characters with varied operations. After each burst of typing, take a screenshot of Client 1 and Client 2 to verify sync.

DocSynchronizer had synchronous docLock.Wait() in ReceiveSyncMessage and SendSyncMessage that blocked thread pool threads during sync. Changed to async await docLock.WaitAsync(). EditorViewModel.UpdateFormattingState had synchronous WithDocumentLock (docLock.Wait()) that blocked UI thread when called from CursorPosition.set during remote caret updates. Changed to fire-and-forget async UpdateFormattingStateAsync. Key lesson: never use synchronous SemaphoreSlim.Wait() on the UI thread or in paths triggered by rapid operations — always use WaitAsync().

