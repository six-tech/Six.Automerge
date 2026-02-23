# Implementation Plan: CollaborativeEditor Crash & Sync Stability

## Sprint 1: AutomergeDocument & AutomergeSyncState Disposal Safety

Foundation layer — fix the FFI bindings so disposed documents don't segfault. This is the lowest layer and must be fixed first since all higher layers depend on it.

- [ ] Read `libs/Six.Automerge/src/AutomergeDocument.cs` and identify all public methods missing `ThrowIfDisposed()` — confirm the 23+ methods listed in design.md (map writes: `PutBoolean`, `PutInt64`, `PutUInt64`, `PutDouble`, `PutBytes`, `PutNull`, `PutCounter`, `PutTimestamp`; list writes: all `ListInsert*`, `ListRemove`, `ListPut*`, `ListSplice`; text: `SpliceText`, `Mark` overloads; mutations: `Delete`, `Increment` overloads, `ApplyChange`, `ApplyChanges`; sync: `ReceiveSyncMessage`, `GenerateSyncMessage`)
- [ ] Add `ThrowIfDisposed()` as the first line of every identified public method in `AutomergeDocument.cs` — follow the existing pattern used by `PutString()` (line ~488) and `GetString()` (line ~525)
- [ ] Read `libs/Six.Automerge/src/AutomergeSyncState.cs` and add a `bool _disposed` field, a `ThrowIfDisposed()` method, update `Dispose()` to set the flag, and add checks to `Reset()`, `SharedHeads` getter, `Encode()`, and `Decode()`
- [ ] Run `dotnet test tests/Six.Automerge.FFI.Tests.Unit` to verify all 379 FFI tests still pass
- [ ] Run `dotnet build` from repo root to verify full build succeeds with no warnings

## Sprint 2: DocSynchronizer Lock & Thread Safety Fixes

Fix the critical lock gaps in the per-document synchronizer — the most likely crash vector.

- [ ] Read `libs/Six.Automerge.Repo/src/Synchronizer/DocSynchronizer.cs` fully. Identify all locations where `_getDocumentLock()` is called and where the lock is nullable-conditional (`docLock?.Wait()`)
- [ ] Fix `ReceiveSyncMessage()`: (a) make lock acquisition non-nullable — throw `InvalidOperationException` if `_getDocumentLock()` returns null, (b) move `_updateDocument(doc)` callback inside the lock scope so the document state is consistent when the callback fires
- [ ] Fix `SendSyncMessage()`: consolidate the two separate lock acquisitions into a single lock scope covering both `GenerateSyncMessage()` and the conditional `Save()` call
- [ ] Add `ILogger<DocSynchronizer>` — log lock acquire/release at `Debug` level, sync message send/receive at `Debug` level (include DocumentId, PeerId, message byte length), and errors at `Error` level. Accept logger via constructor injection.
- [ ] Update `tests/Six.Automerge.Repo.Tests.Unit/src/Synchronizer/DocSynchronizerTests.cs` — fix any tests broken by the non-nullable lock change (tests may pass null locks that now need to be real `SemaphoreSlim` instances). Run the test project and verify all tests pass.

## Sprint 3: DocHandle Async Fix & Repo Lock Safety

Fix the blocking `NotifyRemoteChange` deadlock and the `Repo.Find()` double-release bug.

- [ ] Read `libs/Six.Automerge.Repo/src/DocHandle.cs` and `libs/Six.Automerge.Repo/src/IDocHandle.cs`. Convert `NotifyRemoteChange(AutomergeDocument document)` from synchronous (blocking `_documentLock.Wait()`) to async (`_documentLock.WaitAsync()`). Update the interface signature if `IDocHandle` declares it. Update the `_updateDocument` delegate in `DocSynchronizer` to be `Func<AutomergeDocument, Task>` and await the callback.
- [ ] Fix the `_state` read in `NotifyRemoteChange` — wrap the `_state != HandleState.Ready` check inside `lock(_stateGate)` to avoid reading stale state
- [ ] Add `ILogger<DocHandle>` — log state transitions at `Information` level, lock acquire/release at `Debug`, disposal at `Debug`. Include DocumentId in all messages.
- [ ] Read `libs/Six.Automerge.Repo/src/Repo.cs` — fix `Find<T>()` method's `_handleLock` double-release: introduce a `bool lockHeld` tracking variable, set to `false` when released early for in-progress finds, check in `finally` block before releasing
- [ ] Add `ILogger<Repo>` logging to all fire-and-forget `Task.Run` blocks — `UpdateDocument`, `RequestDocument`, `OnSynchronizerSyncStateChanged`, `Delete`. Replace silent `catch {}` with `catch(Exception ex) { _logger.LogError(ex, ...) }`. Include operation name and DocumentId.
- [ ] Run `dotnet test tests/Six.Automerge.Repo.Tests.Unit` to verify all 263 Repo tests still pass

## Sprint 4: EditorViewModel Thread Safety & Error Visibility

Fix the UI-layer race condition and make silent errors visible.

- [ ] Read `apps/DemoApp.CollaborativeEditor/src/ViewModels/EditorViewModel.cs`. Change `_isRemoteUpdate` from `bool` to `int` (for `Interlocked` compatibility). Update `RefreshFromDocument()` to use `Interlocked.CompareExchange(ref _isRemoteUpdate, 1, 0)` for setting and `Interlocked.Exchange(ref _isRemoteUpdate, 0)` for unsetting. Update the `DocumentText` setter to check `Volatile.Read(ref _isRemoteUpdate) != 0`.
- [ ] Add `ILogger` to `EditorViewModel` — log text changes at `Debug` (include char count, delete count), remote updates at `Debug`, and any caught exceptions at `Warning`
- [ ] Review `EditorWindow.axaml.cs` for any silent catch blocks — replace with logged warnings. Ensure document create/join/resume operations log their outcomes.
- [ ] Add logging to `CollectionSynchronizer.cs` — add `ILogger<CollectionSynchronizer>`, log `AddDocument`, `OnPeerConnected`, `OnPeerDisconnected` at `Information` level with DocumentId and PeerId
- [ ] Run `dotnet test tests/DemoApp.CollaborativeEditor.Tests` to verify all 61 tests pass
- [ ] Run `dotnet build` from repo root — verify zero warnings, zero errors across the full solution

## Sprint 5: E2E Integration Testing via Aspire

Start the full stack, create documents, type heavily across clients, and verify stability using Aspire observability.

- [ ] Kill any orphaned instances of CollaborativeEditor or MCP server processes. Check `Infra.AppHost/AppHost.cs` — verify CollaborativeEditor replicas are set to at least 3 (currently 4). Build the full solution with `dotnet build`.
- [ ] Start the Aspire host: `dotnet run --project apps/Infra.AppHost`. Wait for all services (Azurite, Server.Silo, Server.Api, and all CollaborativeEditor replicas) to be healthy in the Aspire dashboard.
- [ ] Connect to Client 1 via `six-avalonia-mcp`. Create a new document. Copy the document ID from the UI.
- [ ] Connect to Client 2 via `six-avalonia-mcp`. Join the document using the copied ID. Verify the document loads and text area is visible.
- [ ] In Client 1: type a substantial amount of text via `six-avalonia-mcp` — multiple paragraphs with newlines (`\n`), backspaces (`\b`), and mixed content. Type at least 200+ characters with varied operations. After each burst of typing, take a screenshot of Client 1 and Client 2 to verify sync.
- [ ] Check Aspire dashboard traces and logs — search for any `Error` or `Warning` level entries from the CollaborativeEditor, Repo, DocSynchronizer, or DocHandle loggers. Verify no crashes, no deadlocks, no silent sync failures. Document findings.
- [ ] If any client crashed or stopped syncing: capture the error from Aspire logs, identify the root cause, fix it, rebuild, and re-test. Repeat until all clients survive heavy editing for 2+ minutes.
- [ ] Take final screenshots showing all clients with synced content. Stop the Aspire host.
