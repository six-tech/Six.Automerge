# Requirements: 100% Automerge API Coverage + Rich Text Support

## Overview

Complete the C# bindings for the Automerge 0.7.3 Rust crate to achieve 100% API coverage. This includes wrapping all missing P0 and P1 APIs at the Rust FFI layer, exposing them through the C# high-level API, and adding comprehensive rich text (marks, spans, block markers) support.

## Scope

Three workstreams:
1. **Missing API Coverage** — Wrap all ~30 missing Automerge APIs (P0 + P1) from `API_SURFACE_COVERAGE.md`
2. **Rich Text Support** — Full read+write marks, spans, block markers, and cursor APIs
3. **FileSystem Storage Review** — Verify existing `FileSystemStorageAdapter` against TypeScript reference

---

## Functional Requirements

### FR-1: Counter Operations
- **FR-1.1**: `Increment(obj, key, n)` — Increment a counter value in a map by `n` (positive or negative)
- **FR-1.2**: `Increment(obj, index, n)` — Increment a counter value in a list by `n`
- **FR-1.3**: Counter increment MUST be a CRDT-safe merge operation (not overwrite)

### FR-2: Text Read Operations
- **FR-2.1**: `GetText(obj)` — Read the full text content of a Text object without full document hydration
- **FR-2.2**: `GetTextAtHeads(obj, heads)` — Read text content at a historical point

### FR-3: List Read Operations
- **FR-3.1**: `ListGet(obj, index)` — Read a scalar value at a list index (type-specific: `ListGetString`, `ListGetInt64`, etc.)
- **FR-3.2**: `ListGetObject(obj, index)` — Get the ObjId of a nested object at a list index
- **FR-3.3**: `ListGetAtHeads(obj, index, heads)` — Read list value at historical heads

### FR-4: Map Read Operations (Enhanced)
- **FR-4.1**: `MapGetObject(obj, key)` — Get the ObjId of a nested object at a map key (currently returns None)
- **FR-4.2**: `GetAtHeads(obj, key, heads)` — Read map value at historical heads
- **FR-4.3**: `ListGetAtHeads(obj, index, heads)` — Read list value at historical heads

### FR-5: Conflict Resolution
- **FR-5.1**: `GetAll(obj, key)` — Return all conflicting values for a map key (scalars + object refs)
- **FR-5.2**: `GetAll(obj, index)` — Return all conflicting values for a list index
- **FR-5.3**: `GetAllAtHeads(obj, prop, heads)` — Conflicting values at historical heads

### FR-6: Document Lifecycle (Extended)
- **FR-6.1**: `Fork()` — Fork document at current heads (convenience over `ForkAtHeads(GetHeads())`)
- **FR-6.2**: `LoadWithOptions(bytes, options)` — Load with verification mode and actor override
- **FR-6.3**: `SaveNoCompress()` — Save without compression
- **FR-6.4**: `SaveWithOptions(options)` — Save with configurable compression
- **FR-6.5**: `EmptyChange()` — Create a change with no operations
- **FR-6.6**: `BaseHeads()` — Get the base heads of the current transaction

### FR-7: Rich Text — Marks (Write)
- **FR-7.1**: `Mark(obj, start, end, name, value, expand)` — Apply a formatting mark to a text range
- **FR-7.2**: `Unmark(obj, name, start, end, expand)` — Remove a formatting mark from a text range
- **FR-7.3**: `expand` parameter MUST support: `Before`, `After`, `Both`, `None`
- **FR-7.4**: Mark value MUST support all scalar types (string, bool, int, uint, float, bytes, null, counter, timestamp)

### FR-8: Rich Text — Marks (Read)
- **FR-8.1**: `GetMarks(obj)` — Query all marks on a text object, returning `[{name, value, start, end}]`
- **FR-8.2**: `GetMarksAtHeads(obj, heads)` — Query marks at historical heads

### FR-9: Rich Text — Spans
- **FR-9.1**: `GetSpans(obj)` — Get text content as a sequence of `TextSpan` and `BlockSpan` elements
- **FR-9.2**: `GetSpansAtHeads(obj, heads)` — Get spans at historical heads
- **FR-9.3**: Each `TextSpan` MUST include: text content + active marks
- **FR-9.4**: Each `BlockSpan` MUST include: block ObjId + block type + parents + attributes
- **FR-9.5**: `UpdateSpans(obj, spans)` — Update block structure via minimal diff

### FR-10: Rich Text — Block Markers
- **FR-10.1**: `SplitBlock(obj, index)` — Insert a block marker at index, returns new block's ObjId
- **FR-10.2**: `JoinBlock(obj, index)` — Remove the block marker at index (via delete at index)
- **FR-10.3**: Block properties MUST be settable via existing `PutString(blockId, key, value)` etc.

### FR-11: Cursor APIs
- **FR-11.1**: `GetCursor(obj, index, heads?)` — Get a stable cursor for a position in text
- **FR-11.2**: `GetCursorPosition(obj, cursor, heads?)` — Resolve a cursor to a current index
- **FR-11.3**: Cursors MUST be serializable as byte arrays for persistence

### FR-12: List Mutation (Extended)
- **FR-12.1**: `ListSplice(obj, index, deleteCount, values[])` — Generic list splice with multiple scalar values
- **FR-12.2**: `ListPut(obj, index, value)` — Replace list element at index (type-specific variants)

### FR-13: Historical Queries
- **FR-13.1**: `GetKeysAtHeads(obj, heads)` — Map keys at historical heads
- **FR-13.2**: `LengthAtHeads(obj, heads)` — Collection length at historical heads
- **FR-13.3**: `GetMarksAtHeads(obj, heads)` — Marks at historical heads (covered in FR-8.2)

### FR-14: Iteration & Range Queries
- **FR-14.1**: `Values(obj)` — Iterate all values in a map or list
- **FR-14.2**: `ValuesAtHeads(obj, heads)` — Values at historical heads
- **FR-14.3**: `MapRange(obj, startKey, endKey)` — Range query over map keys
- **FR-14.4**: `ListRange(obj, startIndex, endIndex)` — Range query over list indices

### FR-15: Object Graph Navigation
- **FR-15.1**: `Parents(obj)` — Get parent chain from object to root
- **FR-15.2**: `PathToObject(obj)` — Get the path from root to a specific object

### FR-16: Diff & History (Extended)
- **FR-16.1**: `DiffIncremental()` — Efficient incremental diff (C#-level tracking of last-known heads)

### FR-17: Change Management (Extended)
- **FR-17.1**: `GetChangesAdded(other)` — Get changes in this doc not in other
- **FR-17.2**: `Bundle(hashes)` — Bundle changes into a single binary blob

### FR-18: Sync Protocol (Extended)
- **FR-18.1**: `GetMissingDeps(heads)` — Detect dependency gaps in sync
- **FR-18.2**: `HasOurChanges(syncState)` — Check if local changes have been acknowledged

### FR-19: FileSystem Storage Adapter
- **FR-19.1**: Review existing implementation against TypeScript `NodeFSStorageAdapter` reference
- **FR-19.2**: Verify directory sharding logic matches TypeScript (first 2 chars / remaining / components)
- **FR-19.3**: Verify cache semantics and thread safety
- **FR-19.4**: Ensure existing tests adequately cover all IStorageAdapter methods

---

## Non-Functional Requirements

### NFR-1: API Parity
- The C# wrapper MUST achieve 100% coverage of the Automerge 0.7.3 API surface
- Every new FFI function MUST have a corresponding C# high-level wrapper
- C# API naming MUST follow existing conventions (PascalCase, type-specific method names)

### NFR-2: FFI Design
- New FFI types MUST use Interoptopus-compatible `#[ffi_type]` attributes
- Spans MUST use flat discriminated FFI structs (zero-copy, no JSON serialization)
- All FFI functions MUST handle errors via `Result<T, AutomergeFfiError>`
- Memory ownership MUST be clear: Rust-allocated memory freed by Rust, C#-allocated by C#

### NFR-3: Performance
- Rich text operations (mark/unmark/spans) MUST NOT require full document hydration
- Span reads MUST use zero-copy FFI structs, not JSON serialization
- FileSystem adapter MUST maintain O(1) cached reads

### NFR-4: Thread Safety
- All new C# wrappers MUST be thread-safe (consistent with existing patterns)
- FFI calls MUST be safe to call from multiple C# threads (Rust side uses `&mut self` safely via AutoCommit)

### NFR-5: Testing
- Every new API MUST have unit tests covering: basic operation, edge cases, error handling
- Rich text MUST have integration tests covering: mark → sync → merge → verify
- Test count MUST increase proportionally (target: ~200+ new tests)

### NFR-6: Build Pipeline
- Adding new FFI functions MUST NOT break the incremental build pipeline
- `dotnet build` from repo root MUST trigger Rust compilation + codegen automatically
- Generated `Interop.g.cs` MUST be checked in (not generated at build time on consumer machines)

### NFR-7: Backward Compatibility
- All existing APIs MUST continue to work without changes
- Existing 448 tests MUST continue to pass
- No breaking changes to public API surface

### NFR-8: Code Style
- Rust code MUST follow existing patterns in `doc.rs` (error handling, type conversion)
- C# code MUST follow StyleCop rules, `[PublicAPI]` attributes, 2-space indentation
- C# code MUST use `if(` (no space after keyword) per EditorConfig
