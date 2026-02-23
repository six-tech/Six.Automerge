# Design: Documentation Overhaul for Six.Automerge

## Documentation Site Architecture

### Technology Stack
- **Framework**: Astro Starlight (existing)
- **Theme**: `@six-tech/starlight-theme-six` (existing)
- **Format**: MDX files in `docs/src/content/docs/`
- **Config**: `docs/astro.config.mjs` (sidebar configuration)
- **Skill**: Use `docs-writer` skill for all doc writing

### Current State (12 pages, ~3,700 lines)
```
docs/src/content/docs/
  index.mdx                          (landing page)
  introduction/
    index.mdx                        (project intro)
    setup-and-configuration.mdx      (setup guide)
  automerge/
    index.mdx                        (overview)
    getting-started.mdx              (first steps)
    core-concepts.mdx                (concepts - 423 lines)
    working-with-maps.mdx            (maps - 453 lines)
    working-with-lists.mdx           (lists - 512 lines)
    working-with-text.mdx            (text - 459 lines)
    synchronization.mdx              (sync - 602 lines)
    persistence.mdx                  (save/load - 619 lines)
    doc-handles.mdx                  (repo handles - 186 lines)
```

## New Documentation Structure

### Sidebar Navigation (3-level hierarchy)

```
docs/src/content/docs/
│
├── index.mdx                                    (landing page - keep)
│
├── introduction/
│   ├── index.mdx                                (keep)
│   └── setup-and-configuration.mdx              (keep)
│
├── guides/                                       [NEW SECTION]
│   ├── index.mdx                                 (guides overview)
│   ├── getting-started.mdx                       (restructured from automerge/getting-started)
│   ├── core-concepts.mdx                         (restructured from automerge/core-concepts)
│   │
│   ├── maps/
│   │   ├── basic-operations.mdx                  (create, put, get, delete)
│   │   ├── nested-maps.mdx                       (nested containers, object ids)
│   │   ├── counters-and-timestamps.mdx           (counter, timestamp, increment)
│   │   └── advanced-queries.mdx                  (MapRange, MapRangeFull, Values, Parents)
│   │
│   ├── lists/
│   │   ├── basic-operations.mdx                  (insert, remove, length)
│   │   ├── list-splice.mdx                       (ListSplice batch operations)
│   │   ├── list-put.mdx                          (ListPut* index-based updates)
│   │   └── advanced-queries.mdx                  (ListRange, ListRangeFull, Values)
│   │
│   ├── text/
│   │   ├── basic-operations.mdx                  (SpliceText, GetText, text vs string)
│   │   ├── collaborative-editing.mdx             (concurrent edits, merge behavior)
│   │   └── cursors.mdx                           (GetCursor, GetCursorPosition, persistence)
│   │
│   ├── rich-text/
│   │   ├── index.mdx                             (rich text overview, Peritext intro)
│   │   ├── marks.mdx                             (Mark, Unmark, mark value types)
│   │   ├── expand-modes.mdx                      (ExpandMode semantics, boundary behavior)
│   │   ├── reading-marks.mdx                     (GetMarks, GetMarksAtHeads)
│   │   ├── spans.mdx                             (GetSpans, GetSpansAtHeads, span model)
│   │   ├── blocks.mdx                            (SplitBlock, JoinBlock, block types)
│   │   ├── update-spans.mdx                      (UpdateSpans read-modify-write)
│   │   └── concurrent-rich-text.mdx              (multi-peer marks, cross-doc merge)
│   │
│   ├── history/
│   │   ├── commits-and-heads.mdx                 (Commit, GetHeads, BaseHeads, EmptyChange)
│   │   ├── time-travel.mdx                       (AtHeads queries, HydrateJsonAtHeads)
│   │   ├── diffing.mdx                           (Diff, DiffIncremental, patches)
│   │   ├── change-metadata.mdx                   (GetChangeMetadata, TopoHistoryTraversal)
│   │   └── isolate-integrate.mdx                 (Isolate, Integrate, ViewAtHeads)
│   │
│   ├── branching/
│   │   ├── fork-and-merge.mdx                    (Fork, Merge, independent branches)
│   │   ├── fork-at-heads.mdx                     (ForkAtHeads, concurrent branches)
│   │   └── clone.mdx                             (Clone, document duplication)
│   │
│   ├── conflicts/
│   │   ├── understanding-conflicts.mdx           (when conflicts occur, CRDT resolution)
│   │   ├── detecting-conflicts.mdx               (GetAll, GetAllAtHeads)
│   │   └── conflict-values.mdx                   (AutomergeConflictValue, resolution strategies)
│   │
│   ├── persistence/
│   │   ├── save-and-load.mdx                     (Save, Load, basic persistence)
│   │   ├── incremental-saves.mdx                 (SaveIncremental, LoadIncremental, SaveAfter)
│   │   ├── save-options.mdx                      (SaveWithOptions, LoadWithOptions, compression)
│   │   └── storage-strategies.mdx                (full vs incremental vs hybrid)
│   │
│   ├── sync/
│   │   ├── sync-protocol.mdx                     (CreateSyncState, GenerateSyncMessage, ReceiveSyncMessage)
│   │   ├── multi-peer-sync.mdx                   (N-peer sync, network topologies)
│   │   ├── sync-state-management.mdx             (encode/decode, reset, SharedHeads)
│   │   └── advanced-sync.mdx                     (Bundle, GetChangesAdded, GetMissingDeps, HasOurChanges)
│   │
│   └── object-graph/
│       ├── navigation.mdx                        (Parents, PathToObject)
│       └── container-types.mdx                   (GetObjectType, nested containers)
│
├── repo/                                         [NEW SECTION]
│   ├── index.mdx                                 (Repo layer overview)
│   ├── getting-started.mdx                       (Repo setup and basic usage)
│   ├── doc-handles.mdx                           (restructured from automerge/doc-handles)
│   ├── storage/
│   │   ├── index.mdx                             (IStorageAdapter interface)
│   │   ├── in-memory.mdx                         (InMemoryStorageAdapter)
│   │   └── file-system.mdx                       (FileSystemStorageAdapter)
│   ├── network/
│   │   ├── index.mdx                             (INetworkAdapter interface)
│   │   ├── in-memory.mdx                         (InMemoryNetworkAdapter)
│   │   └── signalr.mdx                           (SignalR client/server)
│   └── synchronizer.mdx                          (CollectionSynchronizer, DocSynchronizer)
│
├── examples/                                     [NEW SECTION]
│   ├── index.mdx                                 (examples overview)
│   ├── collaborative-editor.mdx                  (DemoApp.CollaborativeEditor walkthrough)
│   ├── task-board.mdx                            (DemoApp.TaskBoard walkthrough)
│   ├── collaborative-text-editor.mdx             (DemoApp.CollaborativeTextEditor walkthrough)
│   └── figma-clone.mdx                           (DemoApp.FigmaClone walkthrough)
│
└── api/                                          [NEW SECTION]
    ├── index.mdx                                 (API reference overview)
    ├── automerge-document.mdx                    (all AutomergeDocument methods)
    ├── automerge-sync-state.mdx                  (AutomergeSyncState)
    ├── automerge-cursor.mdx                      (AutomergeCursor)
    ├── automerge-spans.mdx                       (AutomergeSpan, AutomergeTextSpan, AutomergeBlockSpan)
    ├── automerge-marks.mdx                       (AutomergeMarkInfo, ExpandMode)
    ├── automerge-patches.mdx                     (AutomergePatch, AutomergePatchAction)
    ├── automerge-conflicts.mdx                   (AutomergeConflictValue, AutomergeScalarValue)
    ├── automerge-options.mdx                     (SaveOptions, LoadOptions, DecodedChange)
    ├── repo.mdx                                  (Repo class)
    ├── doc-handle.mdx                            (IDocHandle, DocHandle)
    ├── storage.mdx                               (IStorageAdapter, StorageKey, Chunk, ChunkInfo)
    ├── network.mdx                               (INetworkAdapter, PeerMetadata, message types)
    └── identifiers.mdx                           (DocumentId, PeerId, StorageKey, AutomergeUrl)
```

### Page Count Summary
| Section | Pages | Description |
|---------|-------|-------------|
| Introduction | 2 | Keep as-is |
| Guides | ~38 | Core API docs restructured + new content |
| Repo | ~8 | Repository layer documentation |
| Examples | 5 | Example app walkthroughs |
| API Reference | 14 | Method-level reference |
| **Total** | **~67** | Up from 12 pages |

## Sidebar Configuration Design

The `astro.config.mjs` sidebar will use nested groups:

```javascript
sidebar: [
  {
    label: 'Getting Started',
    items: [
      { label: 'Overview', slug: 'guides' },
      { label: 'Quick Start', slug: 'guides/getting-started' },
      { label: 'Core Concepts', slug: 'guides/core-concepts' },
    ],
  },
  {
    label: 'Data Types',
    items: [
      {
        label: 'Maps',
        items: [
          { label: 'Basic Operations', slug: 'guides/maps/basic-operations' },
          { label: 'Nested Maps', slug: 'guides/maps/nested-maps' },
          { label: 'Counters & Timestamps', slug: 'guides/maps/counters-and-timestamps' },
          { label: 'Advanced Queries', slug: 'guides/maps/advanced-queries' },
        ],
      },
      {
        label: 'Lists',
        items: [
          { label: 'Basic Operations', slug: 'guides/lists/basic-operations' },
          { label: 'Batch Operations (Splice)', slug: 'guides/lists/list-splice' },
          { label: 'Index Updates (Put)', slug: 'guides/lists/list-put' },
          { label: 'Advanced Queries', slug: 'guides/lists/advanced-queries' },
        ],
      },
      {
        label: 'Text',
        items: [
          { label: 'Basic Operations', slug: 'guides/text/basic-operations' },
          { label: 'Collaborative Editing', slug: 'guides/text/collaborative-editing' },
          { label: 'Cursors', slug: 'guides/text/cursors' },
        ],
      },
    ],
  },
  {
    label: 'Rich Text',
    items: [
      { label: 'Overview', slug: 'guides/rich-text' },
      { label: 'Marks', slug: 'guides/rich-text/marks' },
      { label: 'Expand Modes', slug: 'guides/rich-text/expand-modes' },
      { label: 'Reading Marks', slug: 'guides/rich-text/reading-marks' },
      { label: 'Spans', slug: 'guides/rich-text/spans' },
      { label: 'Blocks', slug: 'guides/rich-text/blocks' },
      { label: 'UpdateSpans', slug: 'guides/rich-text/update-spans' },
      { label: 'Concurrent Rich Text', slug: 'guides/rich-text/concurrent-rich-text' },
    ],
  },
  {
    label: 'Version Control',
    items: [
      {
        label: 'History',
        items: [
          { label: 'Commits & Heads', slug: 'guides/history/commits-and-heads' },
          { label: 'Time Travel', slug: 'guides/history/time-travel' },
          { label: 'Diffing', slug: 'guides/history/diffing' },
          { label: 'Change Metadata', slug: 'guides/history/change-metadata' },
          { label: 'Isolate & Integrate', slug: 'guides/history/isolate-integrate' },
        ],
      },
      {
        label: 'Branching',
        items: [
          { label: 'Fork & Merge', slug: 'guides/branching/fork-and-merge' },
          { label: 'Fork at Heads', slug: 'guides/branching/fork-at-heads' },
          { label: 'Clone', slug: 'guides/branching/clone' },
        ],
      },
      {
        label: 'Conflicts',
        items: [
          { label: 'Understanding Conflicts', slug: 'guides/conflicts/understanding-conflicts' },
          { label: 'Detecting Conflicts', slug: 'guides/conflicts/detecting-conflicts' },
          { label: 'Conflict Values', slug: 'guides/conflicts/conflict-values' },
        ],
      },
    ],
  },
  {
    label: 'Persistence & Sync',
    items: [
      {
        label: 'Persistence',
        items: [
          { label: 'Save & Load', slug: 'guides/persistence/save-and-load' },
          { label: 'Incremental Saves', slug: 'guides/persistence/incremental-saves' },
          { label: 'Save Options', slug: 'guides/persistence/save-options' },
          { label: 'Storage Strategies', slug: 'guides/persistence/storage-strategies' },
        ],
      },
      {
        label: 'Synchronization',
        items: [
          { label: 'Sync Protocol', slug: 'guides/sync/sync-protocol' },
          { label: 'Multi-Peer Sync', slug: 'guides/sync/multi-peer-sync' },
          { label: 'Sync State Management', slug: 'guides/sync/sync-state-management' },
          { label: 'Advanced Sync', slug: 'guides/sync/advanced-sync' },
        ],
      },
    ],
  },
  {
    label: 'Object Graph',
    items: [
      { label: 'Navigation', slug: 'guides/object-graph/navigation' },
      { label: 'Container Types', slug: 'guides/object-graph/container-types' },
    ],
  },
  {
    label: 'Repository',
    items: [
      { label: 'Overview', slug: 'repo' },
      { label: 'Getting Started', slug: 'repo/getting-started' },
      { label: 'Doc Handles', slug: 'repo/doc-handles' },
      {
        label: 'Storage',
        items: [
          { label: 'Interface', slug: 'repo/storage' },
          { label: 'In-Memory', slug: 'repo/storage/in-memory' },
          { label: 'File System', slug: 'repo/storage/file-system' },
        ],
      },
      {
        label: 'Network',
        items: [
          { label: 'Interface', slug: 'repo/network' },
          { label: 'In-Memory', slug: 'repo/network/in-memory' },
          { label: 'SignalR', slug: 'repo/network/signalr' },
        ],
      },
      { label: 'Synchronizer', slug: 'repo/synchronizer' },
    ],
  },
  {
    label: 'Examples',
    items: [
      { label: 'Overview', slug: 'examples' },
      { label: 'Collaborative Editor', slug: 'examples/collaborative-editor' },
      { label: 'Task Board', slug: 'examples/task-board' },
      { label: 'Text Editor', slug: 'examples/collaborative-text-editor' },
      { label: 'Figma Clone', slug: 'examples/figma-clone' },
    ],
  },
  {
    label: 'API Reference',
    items: [
      { label: 'Overview', slug: 'api' },
      { label: 'AutomergeDocument', slug: 'api/automerge-document' },
      { label: 'AutomergeSyncState', slug: 'api/automerge-sync-state' },
      { label: 'AutomergeCursor', slug: 'api/automerge-cursor' },
      { label: 'Spans', slug: 'api/automerge-spans' },
      { label: 'Marks & ExpandMode', slug: 'api/automerge-marks' },
      { label: 'Patches', slug: 'api/automerge-patches' },
      { label: 'Conflicts', slug: 'api/automerge-conflicts' },
      { label: 'Options & Types', slug: 'api/automerge-options' },
      { label: 'Repo', slug: 'api/repo' },
      { label: 'DocHandle', slug: 'api/doc-handle' },
      { label: 'Storage', slug: 'api/storage' },
      { label: 'Network', slug: 'api/network' },
      { label: 'Identifiers', slug: 'api/identifiers' },
    ],
  },
  {
    label: 'Project',
    items: [
      { label: 'About', slug: 'introduction' },
      { label: 'Setup', slug: 'introduction/setup-and-configuration' },
    ],
  },
],
```

## Example Application Architecture

### DemoApp.CollaborativeEditor (Rich Text)

```
apps/DemoApp.CollaborativeEditor/
├── DemoApp.CollaborativeEditor.csproj
├── README.md
├── Program.cs                    (Avalonia entry point)
├── App.axaml / App.axaml.cs
├── ViewModels/
│   └── EditorViewModel.cs        (document state, formatting commands)
├── Views/
│   └── EditorView.axaml          (toolbar + text area)
├── Services/
│   ├── AutomergeService.cs       (AutomergeDocument wrapper)
│   └── SyncService.cs            (SignalR sync integration)
└── Models/
    └── FormattingState.cs        (current mark state)
```

**Key APIs demonstrated**: Mark, Unmark, SplitBlock, JoinBlock, GetSpans, UpdateSpans, ExpandMode, CreateSyncState, GenerateSyncMessage, ReceiveSyncMessage.

### DemoApp.TaskBoard (Kanban)

```
apps/DemoApp.TaskBoard/
├── DemoApp.TaskBoard.csproj
├── README.md
├── Program.cs
├── App.axaml / App.axaml.cs
├── ViewModels/
│   ├── BoardViewModel.cs         (columns, drag-drop)
│   └── TaskCardViewModel.cs      (individual task)
├── Views/
│   ├── BoardView.axaml           (column layout)
│   └── TaskCardView.axaml        (card template)
├── Services/
│   └── AutomergeService.cs
└── Models/
    ├── Column.cs
    └── TaskItem.cs
```

**Key APIs demonstrated**: PutObject (nested maps), ListInsertObject, ListRemove, ListSplice, PutCounter, Increment, GetAll (conflict detection), Merge, Sync.

### DemoApp.CollaborativeTextEditor (Plain Text)

```
apps/DemoApp.CollaborativeTextEditor/
├── DemoApp.CollaborativeTextEditor.csproj
├── README.md
├── Program.cs
├── App.axaml / App.axaml.cs
├── ViewModels/
│   └── TextEditorViewModel.cs    (text state, cursor tracking)
├── Views/
│   └── TextEditorView.axaml      (text input area + peer indicator)
└── Services/
    ├── AutomergeService.cs
    └── CursorService.cs          (cursor persistence)
```

**Key APIs demonstrated**: SpliceText, GetText, GetCursor, GetCursorPosition, AutomergeCursor.ToBytes/FromBytes, Save/Load/SaveIncremental, Sync.

### DemoApp.FigmaClone (Collaborative Design)

```
apps/DemoApp.FigmaClone/
├── DemoApp.FigmaClone.csproj
├── README.md
├── Program.cs
├── App.axaml / App.axaml.cs
├── ViewModels/
│   ├── CanvasViewModel.cs        (canvas objects, selection)
│   └── ToolbarViewModel.cs       (shape tools)
├── Views/
│   ├── CanvasView.axaml          (drawing surface)
│   └── LayerPanel.axaml          (layer list)
├── Services/
│   ├── AutomergeService.cs
│   └── SyncService.cs
└── Models/
    ├── CanvasObject.cs           (shape, position, color)
    └── Layer.cs
```

**Key APIs demonstrated**: PutObject (nested maps for objects), PutDouble (coordinates), PutString (colors), ListInsertObject (layers), Fork/Merge (undo), Diff (change detection), Sync.

## Page Content Design

### Guide Page Template

```mdx
---
title: [Page Title]
description: [Brief description for SEO/sidebar]
---

# [Page Title]

[1-2 paragraph introduction explaining the concept]

## [Primary Concept]

[Explanation of the concept with CRDT context where relevant]

```csharp
// Complete standalone example with usings
using Six.Automerge;

using var doc = AutomergeDocument.Create();
// ... detailed example
```

## [Secondary Concept / Variation]

[Explanation]

```csharp
// Contextual fragment (no usings needed)
doc.SomeMethod(arg1, arg2);
```

## Notes

- [Important caveat or tip]
- [Performance consideration]
- [Common mistake to avoid]
```

### API Reference Page Template

```mdx
---
title: [Class Name] API Reference
description: Complete API reference for [ClassName]
---

# [ClassName]

[Brief description of the class purpose]

**Namespace**: `Six.Automerge`
**Assembly**: `Six.Automerge`

## Properties

### PropertyName

```csharp
public Type PropertyName { get; }
```

Description of the property.

## Methods

### MethodName(param1, param2)

```csharp
public ReturnType MethodName(Type1 param1, Type2 param2)
```

**Parameters**:
- `param1` — Description
- `param2` — Description

**Returns**: Description of return value.

**Exceptions**:
- `ArgumentNullException` — When param1 is null.
- `ObjectDisposedException` — When the document has been disposed.

**Example**:
```csharp
// Brief usage example
var result = doc.MethodName(value1, value2);
```
```

## Old Page Migration Plan

| Old Page | New Location(s) | Action |
|----------|----------------|--------|
| `automerge/index.mdx` | `guides/index.mdx` | Restructure as guides overview |
| `automerge/getting-started.mdx` | `guides/getting-started.mdx` | Update links, keep content |
| `automerge/core-concepts.mdx` | `guides/core-concepts.mdx` | Update links, keep content |
| `automerge/working-with-maps.mdx` | `guides/maps/basic-operations.mdx` + `guides/maps/nested-maps.mdx` | Split into 2+ pages |
| `automerge/working-with-lists.mdx` | `guides/lists/basic-operations.mdx` | Split into 2+ pages |
| `automerge/working-with-text.mdx` | `guides/text/basic-operations.mdx` + `guides/text/collaborative-editing.mdx` | Split into 2+ pages |
| `automerge/synchronization.mdx` | `guides/sync/sync-protocol.mdx` + `guides/sync/multi-peer-sync.mdx` | Split into 4 pages |
| `automerge/persistence.mdx` | `guides/persistence/save-and-load.mdx` + others | Split into 4 pages |
| `automerge/doc-handles.mdx` | `repo/doc-handles.mdx` | Move to repo section |

Old files in `automerge/` directory should be deleted after migration to avoid duplication.

## Test File to Documentation Mapping

| Test File | Documentation Pages |
|-----------|-------------------|
| `AutomergeMapOperationsTests.cs` | maps/basic-operations, maps/counters-and-timestamps |
| `AutomergeListOperationsTests.cs` | lists/basic-operations |
| `AutomergeListMutationAndCursorTests.cs` | lists/list-splice, lists/list-put, text/cursors |
| `AutomergeTextOperationsTests.cs` | text/basic-operations |
| `AutomergeMarkTests.cs` | rich-text/marks, rich-text/expand-modes |
| `AutomergeMarksReadTests.cs` | rich-text/reading-marks, rich-text/spans |
| `AutomergeBlockTests.cs` | rich-text/blocks, rich-text/update-spans |
| `AutomergeBlockIntegrationTests.cs` | rich-text/concurrent-rich-text |
| `AutomergeRichTextIntegrationTests.cs` | rich-text/concurrent-rich-text |
| `AutomergeCrossDocMergeTests.cs` | rich-text/concurrent-rich-text, conflicts/* |
| `AutomergeCursorPersistenceTests.cs` | text/cursors |
| `AutomergeHistoricalQueryTests.cs` | history/time-travel, object-graph/navigation |
| `AutomergeSaveLoadSyncTests.cs` | persistence/save-options, sync/advanced-sync |
| `AutomergeForkTests.cs` | branching/fork-and-merge, branching/fork-at-heads |
| `AutomergeSyncTests.cs` | sync/sync-state-management |
| `AutomergePersistenceTests.cs` | persistence/incremental-saves |
| `AutomergeHistoryTests.cs` | history/commits-and-heads, history/diffing, history/change-metadata |
| `AutomergeNestedObjectsTests.cs` | maps/nested-maps, object-graph/container-types |
| `AutomergeNewApiTests.cs` | multiple pages (comprehensive coverage) |
| `AutomergeApiSurfaceVerificationTests.cs` | api/* reference pages |
