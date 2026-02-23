# Requirements: Documentation Overhaul for Six.Automerge

## Background

Six.Automerge provides C# bindings for Automerge CRDTs. Over the last 10 sprints, the API surface expanded from basic CRUD operations to include rich text (marks, blocks, spans), cursors, historical queries, conflict resolution, advanced persistence, forking, and more. The current documentation (12 pages, ~3,700 lines) covers only the foundational APIs. Approximately 60% of the public API surface is undocumented.

## Functional Requirements

### FR-1: Restructure Existing Documentation

- **FR-1.1**: Split all existing long pages (400+ lines) into logical multi-page groups. Each page should focus on one concept with one detailed example.
- **FR-1.2**: Reorganize the sidebar navigation into a 3-level hierarchy: Topic Group > Sub-Section (Guide/Examples/API) > Individual Pages.
- **FR-1.3**: Maintain all existing content and examples — restructure, don't delete.
- **FR-1.4**: Update the `astro.config.mjs` sidebar configuration to reflect the new page structure.

### FR-2: Document All Undocumented APIs

The following API groups currently have zero documentation and must be fully covered:

- **FR-2.1**: Rich Text — Marks (`Mark`, `Unmark`, `GetMarks`, `GetMarksAtHeads`), Expand Modes (`ExpandMode.Before/After/Both/None`), Spans (`GetSpans`, `GetSpansAtHeads`, `UpdateSpans`), Blocks (`SplitBlock`, `JoinBlock`).
- **FR-2.2**: Cursors — `GetCursor`, `GetCursorPosition`, `AutomergeCursor.FromBytes`/`ToBytes`, cursor persistence across save/load cycles, cursor behavior during concurrent edits.
- **FR-2.3**: Conflict Resolution — `GetAll` (map and list overloads), `GetAllAtHeads`, `Values`, `ValuesAtHeads`, conflict detection and resolution strategies.
- **FR-2.4**: Historical Queries — `GetStringAtHeads`, `ListGetStringAtHeads`, `GetKeysAtHeads`, `LengthAtHeads`, `HydrateJsonAtHeads`, `GetChangeMetadata`, `TopoHistoryTraversal`, `Diff` between heads.
- **FR-2.5**: Document Branching — `Fork`, `ForkAtHeads`, `Clone`, `ViewAtHeads`, `Isolate`/`Integrate`, `Merge`.
- **FR-2.6**: Advanced List Operations — `ListSplice`, `ListPut*` (String, Boolean, Int64, UInt64, Double, Bytes, Null, Object), `ListRange`, `ListRangeFull`.
- **FR-2.7**: Advanced Map Operations — `MapRange`, `MapRangeFull`, `Increment` (counter), `Parents`, `PathToObject`.
- **FR-2.8**: Advanced Persistence — `SaveAfter`, `SaveNoCompress`, `SaveWithOptions`/`LoadWithOptions`, `SaveOptions`, `LoadOptions`, `DiffIncremental`.
- **FR-2.9**: Advanced Sync — `Bundle`, `GetChangesAdded`, `GetMissingDeps`, `HasOurChanges`, sync state encode/decode/reset, `SharedHeads`.
- **FR-2.10**: Misc — `EmptyChange`, `BaseHeads`, `PendingOperations`, `ImmutableString`/`PutImmutableString`/`GetImmutableString`.

### FR-3: Separate API Reference Section

- **FR-3.1**: Create a dedicated API Reference section with method signatures, parameter descriptions, return types, and exceptions for every public method on `AutomergeDocument`.
- **FR-3.2**: Create API reference pages for `AutomergeSyncState`, `AutomergeCursor`, `AutomergeSpan`/`AutomergeTextSpan`/`AutomergeBlockSpan`, `AutomergeMarkInfo`, `AutomergePatch`, `AutomergeConflictValue`, `ExpandMode`, `SaveOptions`, `LoadOptions`, `DecodedChange`.
- **FR-3.3**: Create API reference pages for Repo layer: `Repo`, `IDocHandle`/`DocHandle`, `IStorageAdapter`, `INetworkAdapter`, `DocumentId`, `PeerId`, `StorageKey`.
- **FR-3.4**: Group API reference pages by class/namespace, not alphabetically.

### FR-4: Comprehensive Examples

- **FR-4.1**: Every documentation page must have at least one detailed, working code example.
- **FR-4.2**: The first example on each page must be a standalone, runnable snippet (complete with `using` statements and setup). Subsequent examples on the same page may be contextual fragments.
- **FR-4.3**: Examples must range from basic to advanced complexity within each topic.
- **FR-4.4**: Use existing test files as the primary source for example code patterns (20+ test files with 200+ test methods).

### FR-5: Example Application Projects

Create actual runnable C# projects in the `apps/` directory:

- **FR-5.1**: `DemoApp.CollaborativeEditor` — Avalonia UI rich text editor using marks, blocks, spans, and multi-peer sync. Demonstrates real-time collaborative editing with formatting.
- **FR-5.2**: `DemoApp.TaskBoard` — Avalonia UI task board (Kanban-style) using maps for columns, lists for tasks, counters for votes. Demonstrates structural CRDT patterns with multi-peer sync.
- **FR-5.3**: `DemoApp.CollaborativeTextEditor` — Avalonia UI plain text collaborative editor. Simpler than FR-5.1 — focuses on text CRDTs, cursors, and sync without rich text formatting.
- **FR-5.4**: `DemoApp.FigmaClone` — Avalonia UI collaborative drawing/design app. Uses maps for canvas objects (position, size, color, z-order), lists for layers, and multi-peer sync for real-time collaboration.
- **FR-5.5**: Each example app must include a README documenting how to run it and what it demonstrates.
- **FR-5.6**: Example apps should use Aspire AppHost integration where applicable (at minimum, the SignalR server for network sync).

### FR-6: Rich Text Editor Guide

- **FR-6.1**: Multi-page guide building toward a complete collaborative rich text editor using Avalonia UI.
- **FR-6.2**: Cover the full pipeline: text creation > mark application (bold, italic, underline, links) > block structure (paragraphs, headings, list items) > concurrent editing > sync.
- **FR-6.3**: Explain Peritext semantics and `ExpandMode` behavior with visual diagrams or examples.
- **FR-6.4**: Document mark persistence across save/load and sync cycles.
- **FR-6.5**: Show how spans represent the rendered view of marked text.
- **FR-6.6**: Demonstrate the `UpdateSpans` read-modify-write pattern for programmatic editing.

### FR-7: Repo Layer Documentation

- **FR-7.1**: Document the `Repo` class with all configuration options and lifecycle.
- **FR-7.2**: Document `IDocHandle` state machine (Loading > Ready > Unloaded/Deleted/Unavailable).
- **FR-7.3**: Document `IStorageAdapter` interface and implementations (InMemory, FileSystem).
- **FR-7.4**: Document `INetworkAdapter` interface and implementations (InMemory, SignalR).
- **FR-7.5**: Document the sync protocol as implemented by `CollectionSynchronizer`/`DocSynchronizer`.
- **FR-7.6**: Document `DocumentId`, `PeerId`, `StorageKey`, and `AutomergeUrl` types.

## Non-Functional Requirements

### NFR-1: Navigation and Discoverability

- **NFR-1.1**: Sidebar hierarchy must be at most 3 levels deep.
- **NFR-1.2**: Every page must be reachable within 2 clicks from the sidebar root.
- **NFR-1.3**: Pages within a topic must follow a logical learning progression (concepts > guide > examples > API reference).

### NFR-2: Documentation Quality

- **NFR-2.1**: All code examples must compile against the current API (use `Six.Automerge` namespace, not deprecated names).
- **NFR-2.2**: Use Starlight/Astro MDX format with proper frontmatter (`title`, `description`, optional `sidebar` configuration).
- **NFR-2.3**: Brief CRDT concept primers where relevant, linking to [automerge.org](https://automerge.org/) for deep theory.
- **NFR-2.4**: Pages should be focused: one primary concept per page, one detailed example per page.
- **NFR-2.5**: Target audience is C# developers who may be new to CRDTs. Explain concepts alongside API usage.

### NFR-3: Consistency

- **NFR-3.1**: All example code must use the same coding style as the project (2-space indent, `if(` without space, file-scoped namespaces).
- **NFR-3.2**: Use consistent terminology: "document" (not "doc"), "container" (not "object"), "heads" (not "versions").
- **NFR-3.3**: All pages must follow the same structure: brief intro > concept explanation > code example > notes/caveats.

### NFR-4: Maintainability

- **NFR-4.1**: Example projects must build with `dotnet build` from the repo root.
- **NFR-4.2**: Documentation site must build with existing Starlight/Astro config (only sidebar changes to `astro.config.mjs`).
- **NFR-4.3**: Use the `docs-writer` skill for all documentation writing.
