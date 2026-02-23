# Implementation Plan: DemoApp.CollaborativeEditor Refactoring

## Sprint 1: Cleanup & Sync Throttling Foundation

- [ ] Delete `apps/DemoApp.CollaborativeTextEditor/` project directory entirely
- [ ] Delete `tests/DemoApp.CollaborativeTextEditor.Tests/` test project directory entirely
- [ ] Remove any references to CollaborativeTextEditor from `infra/Infra.AppHost/AppHost.cs` and any other project files
- [ ] Remove Orleans dependencies from `infra/Infra.AppHost/AppHost.cs` and `apps/Server.Api/` (remove Silo references, Azure Storage emulator, Orleans clustering/grain storage config). Keep Server.Api as a simple ASP.NET Core app with SignalR
- [ ] Add `SyncDebounceRate` property to `RepoConfig` (default 100ms), pass it through to `CollectionSynchronizer` and `DocSynchronizer`
- [ ] Implement deferred-final sync throttle in `DocSynchronizer.NotifyLocalChange()` — use the existing `Throttle<T>` helper or create a per-peer throttle that batches sync messages (100ms default, configurable via `SyncDebounceRate`)
- [ ] Run all existing tests (`dotnet test` from repo root) to verify nothing is broken by cleanup and throttle changes

## Sprint 2: Six.Automerge.TextEdit Library — Core

- [ ] Create `libs/Six.Automerge.TextEdit/Six.Automerge.TextEdit.csproj` with references to `Six.Automerge` and `Six.Automerge.Repo`. Add StyleCop, nullable, TreatWarningsAsErrors, and copyright header consistent with other libs
- [ ] Implement `TextDiff` static class: `ComputeSplice(oldText, newText)` returning `(int Offset, int DeleteCount, string InsertText)`. Extract and clean up the diff algorithm from current `EditorViewModel.ApplyTextChange()`
- [ ] Implement `ChangeDebouncer` class: queues text changes, waits for configurable silence period (default 50ms), then flushes accumulated change via callback. Uses `CancellationTokenSource` timer reset pattern (deferred-final)
- [ ] Implement `FormattedSpan` and `FormattedText` models: `FormattedSpan` has `Text`, `Offset`, `Length`, `Marks` dictionary. `FormattedText` has `Spans` list and `PlainText`. Build `FormattedText.FromSpans(IReadOnlyList<AutomergeSpan>)` factory that converts Automerge spans to the model
- [ ] Implement `CursorTracker` class: `SaveAsync(DocHandle, textObjectId, position)` returns `CursorState`, `RestoreAsync(DocHandle, textObjectId, CursorState)` returns updated position. Wraps Automerge cursor APIs behind async DocHandle.ReadAsync calls
- [ ] Implement `TextDocument` class — core constructor taking `DocHandle<object>` and `TextDocumentOptions`. Wire `DocHandle.Changed` event. Implement `Text` and `FormattedText` cached properties. Implement internal refresh logic that reads text and spans from DocHandle under async lock
- [ ] Implement `TextDocument.ApplyTextChangeAsync(newText)` — uses ChangeDebouncer → TextDiff → DocHandle.ChangeAsync(SpliceText). Updates cached state and fires `TextChanged` event
- [ ] Implement `TextDocument.ApplyMarkAsync()` and `RemoveMarkAsync()` — uses DocHandle.ChangeAsync(Mark/Unmark). Updates cached FormattedText and fires `FormattedTextChanged` event

## Sprint 3: Six.Automerge.TextEdit — Factory Methods & Tests

- [ ] Implement `TextDocument.CreateAsync(Repo, options)` — creates a new document with text field, returns wired TextDocument
- [ ] Implement `TextDocument.OpenAsync(Repo, documentId, options)` — finds existing document, waits for ready, returns wired TextDocument. Returns null if document not found within timeout
- [ ] Implement `TextDocumentOptions` record: `TextFieldKey` (default "content"), `ChangeDebounceMs` (default 50), `SyncDebounceRate` (default 100, maps to RepoConfig), `AutoFlushOnDispose` (default true)
- [ ] Create `tests/Six.Automerge.TextEdit.Tests.Unit/` test project. Write tests for `TextDiff.ComputeSplice` covering: insert, delete, replace, empty strings, unicode, identical strings
- [ ] Write tests for `ChangeDebouncer`: single change flushes after delay, rapid changes batch, flush cancels pending, dispose flushes
- [ ] Write tests for `FormattedText.FromSpans`: plain text, bold span, multiple marks, overlapping marks, empty text
- [ ] Write tests for `TextDocument`: create, apply text change, apply mark, remote change event, cursor save/restore, concurrent local+remote edits
- [ ] Run `dotnet test` on the new test project to verify all tests pass

## Sprint 4: Six.Automerge.SyncServer Library

- [ ] Create `libs/Six.Automerge.SyncServer/Six.Automerge.SyncServer.csproj` with references to `Six.Automerge.Repo` and `Six.Automerge.Repo.Abstractions`. Same StyleCop/nullable setup as other libs
- [ ] Implement `IServerTransport` interface with `SendAsync(PeerId, IRepoMessage)`, `BroadcastAsync(documentId, IRepoMessage)`, and events for `PeerConnected`, `PeerDisconnected`, `MessageReceived`
- [ ] Implement `SyncServerOptions` record: `Storage` (required), `PersistenceInterval` (default 5s), `SyncDebounceRate` (default 100ms), `SaveDebounceRate` (default 100ms), `LoggerFactory`
- [ ] Implement `DebouncedPersistence` class: tracks dirty documents via `ConcurrentDictionary<string, bool>`, runs a `PeriodicTimer` at `PersistenceInterval`, flushes all dirty documents by calling `Repo.Flush()` on each
- [ ] Implement `SyncServer` class: creates internal Repo with passive ShareConfig (`Announce = () => false`, `Access = () => true`), FileSystemStorage, and an in-process `INetworkAdapter` that bridges to `IServerTransport`. Exposes `OnPeerConnectedAsync`, `OnPeerDisconnectedAsync`, `OnMessageReceivedAsync` methods called by transport layer
- [ ] Implement document management methods on SyncServer: `ListDocumentsAsync()` scans storage, `ExportDocumentAsync(id)` calls `Repo.Export()`, `DeleteDocumentAsync(id)` calls `Repo.Delete()`
- [ ] Create `tests/Six.Automerge.SyncServer.Tests.Unit/` test project. Write tests using `InMemoryStorageAdapter` and `InMemoryNetworkAdapter`: server creates document on first sync, server persists on timer, server handles peer disconnect, server lists documents
- [ ] Run `dotnet test` to verify all SyncServer tests pass

## Sprint 5: Refactor Server.Api to Use SyncServer

- [ ] Remove `ServerRepoManager`, `CollaborativeHubAdapter`, `CollaborativeHubAdapterFactory`, and `SignalRServerNetworkAdapter` from `apps/Server.Api/`. These are replaced by SyncServer
- [ ] Create `SignalRServerTransport` class implementing `IServerTransport` in Server.Api: maps SignalR hub context to transport interface methods. Uses `IHubContext<DocumentsHub>` for sending
- [ ] Refactor `DocumentsHub` to delegate to `SyncServer` instead of managing its own Repo: `Handshake()` → `syncServer.OnPeerConnectedAsync()`, `SendMessage()` → `syncServer.OnMessageReceivedAsync()`, `OnDisconnectedAsync()` → `syncServer.OnPeerDisconnectedAsync()`, `ListDocuments()` → `syncServer.ListDocumentsAsync()`
- [ ] Update Server.Api `Program.cs`: register `SyncServer` as singleton + `IHostedService`, configure with FileSystemStorageAdapter and 5s persistence interval, wire `SignalRServerTransport`
- [ ] Update `tests/Server.Api.Tests.Unit/` to use the new SyncServer-based architecture. Rewrite tests that reference removed classes
- [ ] Build Server.Api and run its tests to verify everything compiles and passes

## Sprint 6: Refactor EditorViewModel to Use TextDocument

- [ ] Add `Six.Automerge.TextEdit` reference to `DemoApp.CollaborativeEditor.csproj`. Remove direct `Six.Automerge` reference if only used for AutomergeService (keep if needed for types like AutomergeContainerType)
- [ ] Delete `apps/DemoApp.CollaborativeEditor/src/Services/AutomergeService.cs` — all its functionality is now in Six.Automerge.TextEdit
- [ ] Refactor `EditorViewModel`: replace `AutomergeService` and `DocHandle<object>` fields with single `TextDocument` field. Replace `ApplyTextChange()` with `await _textDocument.ApplyTextChangeAsync(newText)`. Replace `WithDocumentLock()` calls with `TextDocument` async methods. Remove `SaveCursorBeforeEdit`/`RestoreCursorAfterEdit` (handled by TextDocument internally). Add `FormattedText` property for UI binding. Make all methods properly async (no fire-and-forget .Wait())
- [ ] Refactor `EditorWindow.axaml.cs`: `HandleCreateAsync` uses `TextDocument.CreateAsync(repo)`. `HandleJoinAsync` uses `TextDocument.OpenAsync(repo, docId)`. `HandleResumeAsync` uses `TextDocument.OpenAsync(repo, docId)`. Remove direct DocHandle/AutomergeService creation
- [ ] Fix SyncStatus lifecycle: set "Creating..." → "Ready" on create, "Joining..." → "Syncing..." → "Ready" on join, "Resuming..." → "Ready" on resume. Subscribe to RepoService connection state changes to update status dynamically (e.g., "Syncing (3 peers)", "Disconnected", "Reconnecting")
- [ ] Update `FormattingState.cs` model if needed, or remove it if `FormattedText` from TextEdit replaces its functionality
- [ ] Build and run `dotnet test tests/DemoApp.CollaborativeEditor.Tests` — expect failures since tests reference old API. Fix compile errors but don't rewrite tests yet (that's Sprint 8)

## Sprint 7: Rich Text Rendering in Avalonia

- [ ] Create `RichTextEditor` UserControl in `apps/DemoApp.CollaborativeEditor/src/Controls/`: a Panel with overlaid TextBox (transparent foreground for input) and TextBlock (for formatted display). Name controls: `InputTextBox`, `DisplayTextBlock`. Expose `Text` (string), `FormattedText` (FormattedText), `CaretIndex` (int), `SelectionStart`/`SelectionEnd` (int) as styled/dependency properties
- [ ] Implement `FormattedTextRenderer` helper class that converts `FormattedText` → Avalonia `InlineCollection`: for each `FormattedSpan`, create a `Run` with `FontWeight.Bold` if IsBold, `FontStyle.Italic` if IsItalic, `TextDecorations.Underline` if IsUnderline. Call this on FormattedText property change
- [ ] Synchronize caret position between TextBox (input layer) and TextBlock (display layer): when TextBox.CaretIndex changes, position a visible caret indicator in the display layer. Ensure selection highlight is visible
- [ ] Replace `<TextBox Name="LocalEditor" .../>` in `EditorWindow.axaml` with the new `<controls:RichTextEditor Name="LocalEditor" .../>`. Update bindings: `Text="{Binding DocumentText}"`, `FormattedText="{Binding FormattedText}"`. Update `EditorWindow.axaml.cs` to wire caret/selection events from the new control
- [ ] Update toolbar button bindings: Bold/Italic/Underline toggle buttons should trigger `EditorViewModel.ToggleBoldAsync()` etc., and reflect current formatting state at cursor
- [ ] Build and run the app manually with `dotnet run --project apps/DemoApp.CollaborativeEditor` to verify the rich text control renders correctly. Test typing, formatting, and caret movement visually

## Sprint 8: Rewrite Tests

- [ ] Rewrite `tests/DemoApp.CollaborativeEditor.Tests/src/EditorViewModelTests.cs` for the new TextDocument-based EditorViewModel: test initialization, text changes via TextDocument, mark toggling, remote change handling, cursor position tracking, SyncStatus lifecycle
- [ ] Rewrite `tests/DemoApp.CollaborativeEditor.Tests/src/AutomergeServiceTests.cs` or delete it (AutomergeService is gone). If FormattingState.cs still exists, keep FormattingStateTests; otherwise delete those too
- [ ] Add additional tests for multi-peer scenarios: create TextDocument on repo A, open on repo B (using InMemoryNetworkAdapter), type on A → verify B receives changes, apply mark on A → verify B gets formatted spans
- [ ] Run `dotnet test` from repo root to verify ALL test projects pass (TextEdit, SyncServer, CollaborativeEditor, existing Repo tests, FFI tests)
- [ ] Fix any test failures discovered during the full test run

## Sprint 9: Integration Testing with Aspire

- [ ] Verify `infra/Infra.AppHost/AppHost.cs` is correctly configured: Server.Api with SyncServer, CollaborativeEditor with 4 replicas, no Orleans references
- [ ] Start the full stack with `dotnet run --project infra/Infra.AppHost`. Verify Aspire dashboard shows all resources healthy (api + 4 editor replicas)
- [ ] Use `six-avalonia-mcp` to connect to editor replica 1: create a new document, verify "Ready" status is shown. Copy the document ID
- [ ] Use `six-avalonia-mcp` on replicas 2, 3, 4: join the document using the copied ID. Verify all show "Ready" status and sync with replica 1
- [ ] Test typing on replica 1 (type a paragraph of text with varied content). Verify text appears on replicas 2, 3, 4 within ~500ms. Check Aspire traces for sync message flow
- [ ] Test marks: select text on replica 1, apply bold. Verify bold formatting is visible on all replicas. Repeat for italic and underline
- [ ] Test concurrent editing: type simultaneously on replicas 1 and 2 for 2+ minutes. Verify no crashes, no data loss, documents converge. Check Aspire dashboard for errors/warnings in all services
- [ ] Test crash resilience: kill replica 3 process. Verify replicas 1, 2, 4 continue working without issues. Restart replica 3, rejoin document, verify it syncs back to current state
