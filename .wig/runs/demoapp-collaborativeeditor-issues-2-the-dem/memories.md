# 2026-02-22 01:00:08 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-1-task-7 | Sprint: Cleanup & Sync Throttling Foundation

**Description**: Run all existing tests (`dotnet test` from repo root) to verify nothing is broken by cleanup and throttle changes

Sprint 1 test counts after cleanup: FFI=379, Repo=266 (+3 throttle), FileSystem=20, FileSystem.Integration=9, Server.Api=76 (-1 TextEditorHub), CollabEditor=67, CollabDrawing=49, CollabTaskBoard=35, Shared=86. Total=987. SignalR integration tests (4) fail without Docker/Aspire - pre-existing.

# 2026-02-22 01:36:00 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-2-task-8 | Sprint: Six.Automerge.TextEdit Library — Core

**Description**: Implement `TextDocument.ApplyMarkAsync()` and `RemoveMarkAsync()` — uses DocHandle.ChangeAsync(Mark/Unmark). Updates cached FormattedText and fires `FormattedTextChanged` event

Automerge cursors: GetCursor(objectId, index) — index must be a valid element index (0 to length-1). Position at end of text (text.Length) is out of bounds and causes FFI error. Cursors reference elements, not insertion points.

# 2026-02-22 01:36:00 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-2-task-8 | Sprint: Six.Automerge.TextEdit Library — Core

**Description**: Implement `TextDocument.ApplyMarkAsync()` and `RemoveMarkAsync()` — uses DocHandle.ChangeAsync(Mark/Unmark). Updates cached FormattedText and fires `FormattedTextChanged` event

Six.Automerge.TextEdit.Tests.Unit: 73 tests (19 TextDiff + 8 ChangeDebouncer + 10 FormattedText + 7 CursorTracker + 9 TextDocument + 9 TextDocumentApply + 11 TextDocumentMark). Native lib requires CopyNativeLibs target in csproj for test projects that reference FFI transitively.

# 2026-02-22 01:36:00 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-2-task-8 | Sprint: Six.Automerge.TextEdit Library — Core

**Description**: Implement `TextDocument.ApplyMarkAsync()` and `RemoveMarkAsync()` — uses DocHandle.ChangeAsync(Mark/Unmark). Updates cached FormattedText and fires `FormattedTextChanged` event

DocHandle<T> requires ViewFactory when using ChangeAsync — without it, the DefaultViewFactory throws InvalidOperationException. Always provide `ViewFactory = _ => new object()` when creating DocHandle<object> in tests.

# 2026-02-22 02:02:02 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-3-task-8 | Sprint: Six.Automerge.TextEdit — Factory Methods & Tests

**Description**: Run `dotnet test` on the new test project to verify all tests pass

Six.Automerge.TextEdit.Tests.Unit: 102 tests after Sprint 3 (23 TextDiff, 8 ChangeDebouncer, 11 FormattedText, 7 CursorTracker, 11 TextDocument, 9 TextDocumentApply, 11 TextDocumentMark, 8 TextDocumentCreateAsync, 8 TextDocumentOpenAsync, 6 TextDocumentConcurrent). TextDocumentOptions changed from constructor-based (AutomergeObjectId) to record with TextFieldKey/ChangeDebounceMs/SyncDebounceRate/AutoFlushOnDispose. TextDocument constructor is now internal — use factory methods CreateAsync/OpenAsync. Repo.Find() with no NetworkAdapters hangs forever for non-existent docs — use InMemoryNetworkAdapter in tests. Repository.Repo requires qualified name to avoid namespace collision in TextEdit library.

# 2026-02-22 02:39:08 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-4-task-8 | Sprint: Six.Automerge.SyncServer Library

**Description**: Run `dotnet test` to verify all SyncServer tests pass

Repo.Tests.Unit has 75 pre-existing failures (191 pass, 75 fail out of 266) due to missing CopyNativeLibs target in its .csproj — native lib not copied to test output. Not caused by any SyncServer changes.

# 2026-02-22 02:39:08 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-4-task-8 | Sprint: Six.Automerge.SyncServer Library

**Description**: Run `dotnet test` to verify all SyncServer tests pass

SyncServer library pattern: `using AutomergeRepo = Six.Automerge.Repository.Repo;` alias is REQUIRED whenever code in namespace `Six.Automerge.*` needs to reference the Repo class, due to namespace collision.

# 2026-02-22 03:18:14 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-5-task-4 | Sprint: Refactor Server.Api to Use SyncServer

**Description**: Update Server.Api `Program.cs`: register `SyncServer` as singleton + `IHostedService`, configure with FileSystemStorageAdapter and 5s persistence interval, wire `SignalRServerTransport`

Server.Api refactored to use SyncServer (Sprint 5). Key files: SignalRServerTransport.cs (IServerTransport impl), DocumentsHub.cs (Hub delegating to SyncServer), SyncServerHostedService.cs (graceful shutdown), ServiceCollectionExtensions.cs (AddSyncServer DI registration). PeerId is in namespace Six.Automerge.Repository.Network (not Six.Automerge.Repository). InternalsVisibleTo must go in ItemGroup not PropertyGroup in csproj. Hub<T>.Clients shadows Hub.Clients — use BindingFlags.DeclaredOnly for reflection. IServerTransport events unused in hub-driven flow (#pragma warning disable CS0067). Six.Automerge.Repo.Tests.Unit has pre-existing DllNotFoundException failures (native lib not copied).

# 2026-02-22 03:59:32 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-6-task-7 | Sprint: Refactor EditorViewModel to Use TextDocument

**Description**: Build and run `dotnet test tests/DemoApp.CollaborativeEditor.Tests` — expect failures since tests reference old API. Fix compile errors but don't rewrite tests yet (that's Sprint 8)

Sprint 6 (Refactor EditorViewModel to Use TextDocument) was already completed by previous sprint work. All 47 DemoApp.CollaborativeEditor.Tests pass. The EditorViewModel uses TextDocument directly with SetTextDocument(), ApplyTextChangeAsync via property setter, and Handle.ReadAsync for save/export. FormattingState.cs remains as a bridge between FormattedText spans and VM boolean properties.

# 2026-02-22 05:03:45 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-7-task-6 | Sprint: Rich Text Rendering in Avalonia

**Description**: Build and run the app manually with `dotnet run --project apps/DemoApp.CollaborativeEditor` to verify the rich text control renders correctly. Test typing, formatting, and caret movement visually

Sprint 7 (Rich Text Rendering) was already implemented by a previous context window. All code existed: RichTextEditor UserControl, FormattedTextRenderer, caret sync, EditorWindow integration, toolbar wiring. 66 tests all pass. Runtime verification confirmed formatting works.

# 2026-02-22 05:59:41 UTC | Spec: demoapp-collaborativeeditor-issues-2-the-dem | Task: sprint-8-task-5 | Sprint: Rewrite Tests

**Description**: Fix any test failures discovered during the full test run

- NetworkSubsystem.Send() had a deadlock with InMemoryNetworkAdapter: holding _lock while calling adapter.Send() causes reentrant lock acquisition because InMemoryNetworkAdapter delivers synchronously. Fix: collect target adapters under lock, release lock, send outside.
- TextDocument.OpenAsync on a remote repo can return null even when Find succeeds, because the sync protocol may transfer the empty document shell (marking handle Ready) before the "text" field change arrives in subsequent sync rounds. Use retry logic for OpenAsync in multi-peer tests.
- Six.Automerge.Repo.Tests.Unit was missing the CopyNativeLibs MSBuild target, causing DllNotFoundException for libsix_automerge. Fixed by adding `<Target Name="CopyNativeLibs" BeforeTargets="Build">` to the test csproj.
- Test count updated: Total 1,083 tests across 10 projects (FFI: 379, Repo: 266, FileSystem: 20, TextEdit: 102, SyncServer: 23, Server.Api: 41, Shared: 86, CollaborativeEditor: 82, Drawing: 49, TaskBoard: 35)

