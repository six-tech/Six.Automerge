# Implementation Plan: Documentation Overhaul for Six.Automerge

## Pre-requisites

- Read existing docs in `docs/src/content/docs/` for content to restructure
- Read test files in `tests/Six.Automerge.FFI.Tests.Unit/src/` as example source
- Read source files in `libs/Six.Automerge/src/` for API signatures
- Use `docs-writer` skill for all documentation writing
- All doc pages use MDX format with Starlight frontmatter

## Sprint 1: Scaffold New Directory Structure and Migrate Existing Pages

- [ ] Create the new directory structure under `docs/src/content/docs/`: `guides/`, `guides/maps/`, `guides/lists/`, `guides/text/`, `guides/rich-text/`, `guides/history/`, `guides/branching/`, `guides/conflicts/`, `guides/persistence/`, `guides/sync/`, `guides/object-graph/`, `repo/`, `repo/storage/`, `repo/network/`, `examples/`, `api/`
- [ ] Create `guides/index.mdx` as the new guides overview page (brief intro to all guide sections with links)
- [ ] Move and restructure `automerge/getting-started.mdx` to `guides/getting-started.mdx` (update internal links, keep content)
- [ ] Move and restructure `automerge/core-concepts.mdx` to `guides/core-concepts.mdx` (update internal links, keep content)
- [ ] Split `automerge/working-with-maps.mdx` (453 lines) into `guides/maps/basic-operations.mdx` (create/put/get/delete/keys/nested) and `guides/maps/nested-maps.mdx` (nested containers, object IDs, deep nesting). Source content from existing page + `AutomergeMapOperationsTests.cs` and `AutomergeNestedObjectsTests.cs`
- [ ] Split `automerge/working-with-lists.mdx` (512 lines) into `guides/lists/basic-operations.mdx` (insert/remove/length/nested). Source from existing page + `AutomergeListOperationsTests.cs`
- [ ] Split `automerge/working-with-text.mdx` (459 lines) into `guides/text/basic-operations.mdx` (SpliceText, GetText, text vs string) and `guides/text/collaborative-editing.mdx` (concurrent edits, merge behavior). Source from existing page + `AutomergeTextOperationsTests.cs`
- [ ] Update `docs/astro.config.mjs` sidebar configuration for all Sprint 1 pages (Getting Started, Data Types sections). Delete old `automerge/` files that have been migrated. Keep `automerge/index.mdx` as a redirect or remove it.

## Sprint 2: Map and List Advanced Pages

- [ ] Write `guides/maps/counters-and-timestamps.mdx` — PutCounter, GetCounter, Increment (map overload), PutTimestamp, GetTimestamp. Source examples from `AutomergeMapOperationsTests.cs` (PutCounter, PutTimestamp) and `AutomergeNewApiTests.cs` (Increment_MapCounter_*)
- [ ] Write `guides/maps/advanced-queries.mdx` — MapRange, MapRangeFull, Values, ValuesAtHeads, Parents, PathToObject. Source from `AutomergeHistoricalQueryTests.cs` (MapRange_*, Parents_*, PathToObject_*)
- [ ] Write `guides/lists/list-splice.mdx` — ListSplice batch operations (insert multiple, delete, replace, mixed types). Source from `AutomergeListMutationAndCursorTests.cs` (ListSplice_*)
- [ ] Write `guides/lists/list-put.mdx` — ListPut* index-based updates (String, Boolean, Int64, UInt64, Double, Bytes, Null, Object). Source from `AutomergeListMutationAndCursorTests.cs` (ListPut*_*)
- [ ] Write `guides/lists/advanced-queries.mdx` — ListRange, ListRangeFull, Values for lists, LengthAtHeads. Source from `AutomergeHistoricalQueryTests.cs` (ListRange_*, LengthAtHeads_*)
- [ ] Update `docs/astro.config.mjs` sidebar to include all Sprint 2 pages under Data Types > Maps and Data Types > Lists

## Sprint 3: Text Cursors and Rich Text Introduction

- [ ] Write `guides/text/cursors.mdx` — GetCursor, GetCursorPosition, AutomergeCursor.FromBytes/ToBytes, cursor persistence across save/load, cursor behavior with concurrent inserts before/after. Source from `AutomergeListMutationAndCursorTests.cs` (GetCursor_*, Cursor_*) and `AutomergeCursorPersistenceTests.cs`
- [ ] Write `guides/rich-text/index.mdx` — Rich text overview: what marks, blocks, and spans are, Peritext reference, when to use rich text vs plain text
- [ ] Write `guides/rich-text/marks.mdx` — Mark (string/boolean/int overloads), Unmark, mark value types, applying bold/italic/links. Source from `AutomergeMarkTests.cs`
- [ ] Write `guides/rich-text/expand-modes.mdx` — ExpandMode enum (Before, After, Both, None), boundary behavior when text is inserted at mark edges, visual examples. Source from `AutomergeMarkTests.cs` (Mark_WithEachExpandMode_*)
- [ ] Write `guides/rich-text/reading-marks.mdx` — GetMarks, GetMarksAtHeads, mark persistence across save/load and sync. Source from `AutomergeMarksReadTests.cs`
- [ ] Update `docs/astro.config.mjs` sidebar to include Text > Cursors and Rich Text section

## Sprint 4: Rich Text Advanced Pages

- [ ] Write `guides/rich-text/spans.mdx` — GetSpans, GetSpansAtHeads, AutomergeTextSpan/AutomergeBlockSpan model, how spans represent rendered text with formatting. Source from `AutomergeMarksReadTests.cs` (GetSpans_*)
- [ ] Write `guides/rich-text/blocks.mdx` — SplitBlock, JoinBlock, block types (paragraph, heading, list-item), block metadata (type, parents, attributes). Source from `AutomergeBlockTests.cs`
- [ ] Write `guides/rich-text/update-spans.mdx` — UpdateSpans read-modify-write pattern, minimal diff computation, inserting/removing blocks programmatically. Source from `AutomergeBlockTests.cs` (UpdateSpans_*)
- [ ] Write `guides/rich-text/concurrent-rich-text.mdx` — Multi-peer mark editing, overlapping marks from different peers, mark merge semantics (LWW for same name), cross-doc mark sync. Source from `AutomergeCrossDocMergeTests.cs` and `AutomergeRichTextIntegrationTests.cs`
- [ ] Update `docs/astro.config.mjs` sidebar for remaining Rich Text pages

## Sprint 5: History and Branching Documentation

- [ ] Write `guides/history/commits-and-heads.mdx` — Commit (with message/timestamp), GetHeads, BaseHeads, EmptyChange, PendingOperations, Rollback. Source from `AutomergeHistoryTests.cs`
- [ ] Write `guides/history/time-travel.mdx` — GetStringAtHeads, ListGetStringAtHeads, GetKeysAtHeads, LengthAtHeads, HydrateJsonAtHeads, reading document at any point in history. Source from `AutomergeNewApiTests.cs` (GetStringAtHeads_*) and `AutomergeHistoricalQueryTests.cs`
- [ ] Write `guides/history/diffing.mdx` — Diff between heads, DiffIncremental, AutomergePatch model (action kinds: PutMap, PutSeq, Insert, SpliceText, Increment, etc.). Source from `AutomergeHistoryTests.cs` (Diff_*) and `AutomergeSaveLoadSyncTests.cs` (DiffIncremental_*)
- [ ] Write `guides/history/change-metadata.mdx` — GetChangeMetadata (DecodedChange: hash, actor, sequence, timestamp, message, deps), TopoHistoryTraversal. Source from `AutomergeHistoryTests.cs`
- [ ] Write `guides/history/isolate-integrate.mdx` — Isolate (view at specific heads), Integrate (restore full view), ViewAtHeads convenience method. Source from `AutomergeHistoryTests.cs` (IsolateAndIntegrate_*)
- [ ] Write `guides/branching/fork-and-merge.mdx` — Fork (create independent copy), Merge (combine changes back), independent branches, isolation verification. Source from `AutomergeNewApiTests.cs` (Fork_*) and `AutomergeForkTests.cs`
- [ ] Write `guides/branching/fork-at-heads.mdx` — ForkAtHeads (fork at historical point), concurrent branches, creating what-if scenarios. Source from `AutomergeForkTests.cs` (ForkAtHeads_*)
- [ ] Write `guides/branching/clone.mdx` — Clone (exact duplicate), use cases for cloning vs forking
- [ ] Update `docs/astro.config.mjs` sidebar for Version Control section (History + Branching)

## Sprint 6: Conflicts, Persistence, and Sync Documentation

- [ ] Write `guides/conflicts/understanding-conflicts.mdx` — When conflicts occur in CRDTs, Automerge's automatic resolution, LWW semantics, why manual conflict handling is sometimes needed
- [ ] Write `guides/conflicts/detecting-conflicts.mdx` — GetAll (map and list overloads), GetAllAtHeads, detecting when multiple peers wrote to same key/index. Source from `AutomergeNewApiTests.cs` (GetAll_*)
- [ ] Write `guides/conflicts/conflict-values.mdx` — AutomergeConflictValue model, resolution strategies, displaying conflicts to users
- [ ] Split `automerge/persistence.mdx` (619 lines) into `guides/persistence/save-and-load.mdx` (Save, Load, basic persistence, error handling). Keep relevant content from existing page
- [ ] Write `guides/persistence/incremental-saves.mdx` — SaveIncremental, LoadIncremental, SaveAfter (save changes after specific heads), incremental strategies. Source from `AutomergePersistenceTests.cs` and `AutomergeSaveLoadSyncTests.cs`
- [ ] Write `guides/persistence/save-options.mdx` — SaveWithOptions (SaveOptions: Deflate, RetainOrphans), LoadWithOptions (LoadOptions), SaveNoCompress, size comparisons. Source from `AutomergeSaveLoadSyncTests.cs` (SaveWithOptions_*, LoadWithOptions_*)
- [ ] Write `guides/persistence/storage-strategies.mdx` — Full vs incremental vs hybrid storage patterns, database storage, cloud storage. Restructure from existing persistence page
- [ ] Update `docs/astro.config.mjs` sidebar for Conflicts and Persistence sections

## Sprint 7: Sync and Object Graph Documentation

- [ ] Split `automerge/synchronization.mdx` (602 lines) into `guides/sync/sync-protocol.mdx` (CreateSyncState, GenerateSyncMessage, ReceiveSyncMessage, basic 2-peer sync). Keep relevant content from existing page
- [ ] Write `guides/sync/multi-peer-sync.mdx` — N-peer sync patterns, network topologies (star, mesh), sync helper function. Restructure from existing sync page
- [ ] Write `guides/sync/sync-state-management.mdx` — AutomergeSyncState.Encode/Decode, Reset, SharedHeads, persisting sync state across sessions. Source from `AutomergeSyncTests.cs`
- [ ] Write `guides/sync/advanced-sync.mdx` — Bundle (create sync bundle at specific heads), GetChangesAdded (differential changes), GetMissingDeps (dependency tracking), HasOurChanges (sync completion check). Source from `AutomergeSaveLoadSyncTests.cs`
- [ ] Write `guides/object-graph/navigation.mdx` — Parents (ancestor chain from object to root), PathToObject (visible path), AutomergePathElement. Source from `AutomergeHistoricalQueryTests.cs` (Parents_*, PathToObject_*)
- [ ] Write `guides/object-graph/container-types.mdx` — GetObjectType, AutomergeContainerType enum, working with mixed container hierarchies. Source from `AutomergeNestedObjectsTests.cs`
- [ ] Delete old `automerge/synchronization.mdx` and `automerge/persistence.mdx` files after migration. Clean up any remaining old `automerge/` files
- [ ] Update `docs/astro.config.mjs` sidebar for Sync, Object Graph, and complete cleanup of old automerge section

## Sprint 8: Repo Layer Documentation

- [ ] Write `repo/index.mdx` — Repo layer overview: what it provides over raw AutomergeDocument, architecture diagram, when to use Repo vs standalone documents
- [ ] Write `repo/getting-started.mdx` — Setting up a Repo with storage and network adapters, creating/finding documents, basic Repo lifecycle. Source from `DemoApp.Basic/Program.cs` and Repo.cs
- [ ] Move and restructure `automerge/doc-handles.mdx` to `repo/doc-handles.mdx` — IDocHandle state machine, HandleState enum, WhenReadyAsync, GetDocument, events (DocumentLoaded, Changed, HeadsChanged). Expand significantly beyond current 186 lines
- [ ] Write `repo/storage/index.mdx` — IStorageAdapter interface, Load/Save/Remove/LoadRange/RemoveRange methods, StorageKey, Chunk/ChunkInfo/ChunkType
- [ ] Write `repo/storage/in-memory.mdx` — InMemoryStorageAdapter usage, when to use (testing, ephemeral sessions). Source from Repo tests
- [ ] Write `repo/storage/file-system.mdx` — FileSystemStorageAdapter setup, directory structure, caching behavior, known limitations. Source from `FileSystemStorageAdapter.cs` and storage tests
- [ ] Write `repo/network/index.mdx` — INetworkAdapter interface, events (PeerCandidate, PeerDisconnected, MessageReceived), PeerMetadata
- [ ] Write `repo/network/in-memory.mdx` — InMemoryNetworkAdapter for testing, wiring up multiple repos. Source from Repo tests

## Sprint 9: Repo Network and Synchronizer Documentation

- [ ] Write `repo/network/signalr.mdx` — SignalR client (SignalRNetworkAdapter, SignalRConnectionService) and server (DocumentsHub) setup, hub interfaces, ASP.NET Core integration. Source from SignalR libs and test server
- [ ] Write `repo/synchronizer.mdx` — CollectionSynchronizer and DocSynchronizer, how they orchestrate the Automerge sync protocol across peers, throttling, event flow
- [ ] Write `repo/identifiers.mdx` — DocumentId (Base58Check encoding), PeerId, StorageKey, AutomergeUrl, SessionId. Source from Abstractions lib

## Sprint 10: API Reference — Core Document

- [ ] Write `api/index.mdx` — API Reference overview page, link to all reference pages, explain how to read the reference
- [ ] Write `api/automerge-document.mdx` — Complete method reference for AutomergeDocument (129+ methods). Organize by category: Creation/Disposal, Map Operations, List Operations, Text Operations, Rich Text (Marks/Blocks/Spans), History/Heads, Branching, Persistence, Sync, Object Graph. Include signature, parameters, returns, exceptions, and brief example for each method
- [ ] Write `api/automerge-sync-state.mdx` — AutomergeSyncState: Create, Encode, Decode, Reset, SharedHeads
- [ ] Write `api/automerge-cursor.mdx` — AutomergeCursor: FromBytes, ToBytes, usage with GetCursor/GetCursorPosition

## Sprint 11: API Reference — Types and Repo

- [ ] Write `api/automerge-spans.mdx` — AutomergeSpan (abstract), AutomergeTextSpan (Text, Marks), AutomergeBlockSpan (BlockType, Parents, AttributesJson)
- [ ] Write `api/automerge-marks.mdx` — AutomergeMarkInfo (Name, Value, Start, End), ExpandMode enum, AutomergeScalarValue
- [ ] Write `api/automerge-patches.mdx` — AutomergePatch (ObjectId, Path, Action), AutomergePatchAction, AutomergePatchActionKind enum, AutomergePatchPathElement, AutomergeInsertEntry
- [ ] Write `api/automerge-conflicts.mdx` — AutomergeConflictValue, AutomergeScalarValue, AutomergeScalarValueKind enum
- [ ] Write `api/automerge-options.mdx` — SaveOptions (Deflate, RetainOrphans), LoadOptions, DecodedChange (Hash, Actor, Sequence, Timestamp, Message, Dependencies), ImmutableString, AutomergeContainerType, AutomergePathElement
- [ ] Write `api/repo.mdx` — Repo class: constructor/config, Create/Find/Delete/Clone, events, Peers, Handles, Dispose
- [ ] Write `api/doc-handle.mdx` — IDocHandle interface, HandleState enum, lifecycle methods, sync operations
- [ ] Write `api/storage.mdx` — IStorageAdapter, StorageAdapterBase, StorageKey, Chunk, ChunkInfo, ChunkType
- [ ] Write `api/network.mdx` — INetworkAdapter, events, PeerMetadata, IRepoMessage and all message types (SyncMessage, RequestMessage, DocumentSnapshotMessage, etc.)
- [ ] Write `api/identifiers.mdx` — DocumentId, PeerId, StorageKey, StorageId, SessionId, AutomergeUrl

## Sprint 12: Example App — Collaborative Text Editor

- [ ] Create `apps/DemoApp.CollaborativeTextEditor/` project scaffold: `.csproj` (Avalonia Desktop, references Six.Automerge and Six.Automerge.Repo), `Program.cs`, `App.axaml`
- [ ] Implement `Services/AutomergeService.cs` — wraps AutomergeDocument, provides text CRDT operations (SpliceText, GetText), save/load, cursor tracking (GetCursor/GetCursorPosition)
- [ ] Implement `Services/SyncService.cs` — manages sync state, generates/receives sync messages via InMemoryNetworkAdapter (or SignalR if Aspire available)
- [ ] Implement `ViewModels/TextEditorViewModel.cs` — observable text content, cursor position, peer count, commands for text input and sync
- [ ] Implement `Views/TextEditorView.axaml` — TextBox bound to document text, status bar showing peer count and sync status
- [ ] Write `README.md` for the project explaining how to run it and what APIs it demonstrates
- [ ] Write `examples/collaborative-text-editor.mdx` documentation page with step-by-step walkthrough of the app code and architecture
- [ ] Verify the app builds with `dotnet build apps/DemoApp.CollaborativeTextEditor`

## Sprint 13: Example App — Task Board

- [ ] Create `apps/DemoApp.TaskBoard/` project scaffold: `.csproj` (Avalonia Desktop, references Six.Automerge and Six.Automerge.Repo), `Program.cs`, `App.axaml`
- [ ] Implement `Models/` — Column (map with name, order), TaskItem (map with title, description, status, vote counter)
- [ ] Implement `Services/AutomergeService.cs` — wraps AutomergeDocument, CRUD operations for columns (PutObject) and tasks (ListInsertObject, ListRemove, ListSplice), counter increments
- [ ] Implement `ViewModels/BoardViewModel.cs` — observable columns collection, drag-drop move (ListRemove + ListInsertObject), add/delete columns and tasks
- [ ] Implement `Views/BoardView.axaml` — horizontal column layout with vertical task lists, add/remove buttons, vote counter display
- [ ] Write `README.md` and `examples/task-board.mdx` documentation page
- [ ] Verify the app builds with `dotnet build apps/DemoApp.TaskBoard`

## Sprint 14: Example App — Collaborative Rich Text Editor

- [ ] Create `apps/DemoApp.CollaborativeEditor/` project scaffold: `.csproj` (Avalonia Desktop, references Six.Automerge and Six.Automerge.Repo), `Program.cs`, `App.axaml`
- [ ] Implement `Services/AutomergeService.cs` — wraps AutomergeDocument with rich text operations: SpliceText, Mark/Unmark (bold, italic, underline, link), SplitBlock/JoinBlock, GetSpans, UpdateSpans
- [ ] Implement `Services/SyncService.cs` — multi-peer sync using SignalR or InMemory adapter
- [ ] Implement `Models/FormattingState.cs` — tracks active marks at cursor position for toolbar state
- [ ] Implement `ViewModels/EditorViewModel.cs` — observable formatted text content, formatting toggle commands (bold, italic, etc.), block type selection, sync status
- [ ] Implement `Views/EditorView.axaml` — formatting toolbar (bold/italic/underline/link buttons), text editor area rendering spans with formatting, block type dropdown
- [ ] Write `README.md` and `examples/collaborative-editor.mdx` documentation page with detailed walkthrough
- [ ] Verify the app builds with `dotnet build apps/DemoApp.CollaborativeEditor`

## Sprint 15: Example App — Figma Clone

- [ ] Create `apps/DemoApp.FigmaClone/` project scaffold: `.csproj` (Avalonia Desktop, references Six.Automerge and Six.Automerge.Repo), `Program.cs`, `App.axaml`
- [ ] Implement `Models/` — CanvasObject (map with type, x, y, width, height, color, zOrder), Layer (list of object references)
- [ ] Implement `Services/AutomergeService.cs` — wraps AutomergeDocument for canvas operations: add/move/resize/recolor objects (PutObject, PutDouble, PutString), manage layers (ListInsertObject, ListRemove)
- [ ] Implement `ViewModels/CanvasViewModel.cs` — observable canvas objects, selection state, drag-to-move/resize, Fork for undo (Fork + Merge pattern)
- [ ] Implement `Views/CanvasView.axaml` — drawing surface rendering shapes, selection handles, toolbar for shape types, layer panel
- [ ] Implement `Services/SyncService.cs` — multi-peer sync so multiple users see real-time shape changes
- [ ] Write `README.md` and `examples/figma-clone.mdx` documentation page
- [ ] Verify the app builds with `dotnet build apps/DemoApp.FigmaClone`

## Sprint 16: Examples Overview, Final Sidebar Config, and Polish

- [ ] Write `examples/index.mdx` — examples overview page with cards linking to each example app, describing complexity level and APIs demonstrated
- [ ] Final update of `docs/astro.config.mjs` — complete sidebar configuration covering all ~67 pages, verify all slugs match file paths
- [ ] Verify all old `automerge/` pages have been properly migrated or deleted (no dead links)
- [ ] Review and update `docs/src/content/docs/index.mdx` landing page — update feature cards, update "Get Started" link to new guides path
- [ ] Update `introduction/index.mdx` with links to new documentation structure
- [ ] Spot-check that all code examples use correct API names (Six.Automerge namespace), correct method signatures, and compile-ready patterns
- [ ] Verify the documentation site builds: run `npm run build` or `pnpm build` in `docs/` directory
