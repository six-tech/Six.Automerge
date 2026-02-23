# Implementation Plan: 100% Automerge API Coverage + Rich Text Support

## Sprint 1: Build Pipeline Verification & FileSystem Review

- [ ] Verify Rust FFI build pipeline: run `cargo build --release` in `libs/Six.Automerge.FFI/native/` and confirm it produces the native library successfully
- [ ] Verify Interoptopus codegen: run the codegen tool in `libs/Six.Automerge.FFI/codegen/` and confirm `generated/Interop.g.cs` is regenerated correctly (compare before/after)
- [ ] Verify full `dotnet build` from repo root succeeds and all existing 448 tests pass (`dotnet test tests/Six.Automerge.FFI.Tests.Unit` and `dotnet test tests/Six.Automerge.Repo.Tests.Unit`)
- [ ] Review `FileSystemStorageAdapter` against TypeScript `NodeFSStorageAdapter` (`.references/automerge-repo-2.5.3/packages/automerge-repo-storage-nodefs/src/index.ts`): verify sharding logic, cache semantics, LoadRange deduplication, and RemoveRange behavior
- [ ] Run FileSystem storage tests (`dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit` and `dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Integration`) and document any issues

## Sprint 2: P0 — Counter Increment, Text Read, Fork

- [ ] Add Rust FFI functions in `libs/Six.Automerge.FFI/native/src/doc.rs`: `increment_map(obj, key, value)` and `increment_list(obj, index, value)` calling `self.inner.increment()` from the `Transactable` trait
- [ ] Add Rust FFI functions in `doc.rs`: `text(obj)` and `text_at(obj, heads)` calling `self.inner.text()` / `self.inner.text_at()` from `ReadDoc` trait
- [ ] Add Rust FFI function in `doc.rs`: `fork(doc)` calling `doc.inner.fork()` — returns a new `AutomergeDoc`
- [ ] Add Rust FFI function in `doc.rs`: `empty_change()` calling `self.inner.empty_change()` — returns the change hash; and `base_heads()` calling `self.inner.base_heads()` — returns Vec of change hashes
- [ ] Regenerate `Interop.g.cs` via the codegen pipeline and verify it compiles
- [ ] Add C# wrappers in `libs/Six.Automerge/src/AutomergeDocument.cs`: `Increment(obj, key, delta)`, `Increment(obj, index, delta)`, `GetText(obj)`, `GetTextAtHeads(obj, heads)`, `Fork()`, `EmptyChange()`, `BaseHeads()`
- [ ] Add unit tests in `tests/Six.Automerge.FFI.Tests.Unit/src/`: counter increment (map + list, positive + negative), text read (empty, non-empty, after splice), text_at historical read, fork at current heads, empty_change, base_heads

## Sprint 3: P0 — List/Map Get, Object Access, Historical & Conflict Reads

- [ ] Add Rust FFI functions in `doc.rs`: `list_get(obj, idx)` → `Option<AutomergeScalarValue>` and `list_get_object(obj, idx)` → `Option<AutomergeObjId>` — call `self.inner.get(obj_id, idx)` and pattern-match on `Value::Scalar` vs `Value::Object`
- [ ] Add Rust FFI function in `doc.rs`: `map_get_object(obj, key)` → `Option<AutomergeObjId>` — same pattern as `map_get` but returns ObjId for object values instead of None
- [ ] Add Rust FFI functions in `doc.rs`: `map_get_at(obj, key, heads)` and `list_get_at(obj, idx, heads)` — call `self.inner.get_at(obj_id, prop, &heads)`
- [ ] Add `AutomergeConflictValue` and `AutomergeValue` types to `types.rs` (see design.md); add Rust FFI functions: `map_get_all(obj, key)`, `list_get_all(obj, idx)`, `map_get_all_at(obj, key, heads)`, `list_get_all_at(obj, idx, heads)` — call `self.inner.get_all()` / `self.inner.get_all_at()`
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# wrappers in `AutomergeDocument.cs`: `ListGetString()`, `ListGetInt64()`, `ListGetBoolean()`, `ListGetDouble()`, `ListGetUInt64()`, `ListGetBytes()`, `ListGetObject()`, `GetObject(obj, key)`, `GetStringAtHeads()`, `ListGetStringAtHeads()`, `GetAll(obj, key)`, `GetAll(obj, index)`, `GetAllAtHeads()` — plus new C# types `AutomergeValue`, `AutomergeConflictValue` in separate files
- [ ] Add unit tests: list_get for all types, list_get_object for nested containers, map_get_object, get_at historical reads for maps and lists, get_all conflict detection with 2+ concurrent writers

## Sprint 4: Rich Text — Marks Write API

- [ ] Add `AutomergeExpandMark` enum to `libs/Six.Automerge.FFI/native/src/types.rs` with variants: `Before`, `After`, `Both`, `None` — implement `From<AutomergeExpandMark> for automerge::marks::ExpandMark` conversion
- [ ] Add Rust FFI function in `doc.rs`: `mark(obj, start, end, name, value, expand)` — construct `automerge::marks::Mark { start, end, name, value }` and call `self.inner.mark(obj_id, mark, expand_mode)`
- [ ] Add Rust FFI function in `doc.rs`: `unmark(obj, name, start, end, expand)` — call `self.inner.unmark(obj_id, name, start, end, expand_mode)`
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# types: `ExpandMode` enum in `libs/Six.Automerge/src/ExpandMode.cs`; Add C# wrappers in `AutomergeDocument.cs`: `Mark(obj, start, end, name, value, expand)` and `Unmark(obj, name, start, end, expand)`
- [ ] Add unit tests: mark bold on text range, unmark bold, mark with each expand mode, mark with different value types (string, bool, int), overlapping marks, mark on empty text (error), mark with invalid range (error)

## Sprint 5: Rich Text — Marks & Spans Read API

- [ ] Add Rust FFI functions in `doc.rs`: `marks(obj)` → `Vec<AutomergeMarkSpan>` and `marks_at(obj, heads)` → `Vec<AutomergeMarkSpan>` — call `self.inner.marks(obj_id)` / `self.inner.marks_at(obj_id, &heads)` and convert each `Mark` to existing `AutomergeMarkSpan` type
- [ ] Add `AutomergeSpanKind` enum and `AutomergeSpan` struct to `types.rs` (see design.md) — the flat discriminated union with text+marks fields and block+type+parents fields
- [ ] Add Rust FFI function in `doc.rs`: `spans(obj)` → `Vec<AutomergeSpan>` — call `self.inner.spans(obj_id)` and convert each `Span::Text(s, marks)` / `Span::Block(obj_id)` to `AutomergeSpan`
- [ ] Add Rust FFI function in `doc.rs`: `spans_at(obj, heads)` → `Vec<AutomergeSpan>` — same as above but with heads
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# types in `libs/Six.Automerge/src/`: `AutomergeSpan` (abstract record), `AutomergeTextSpan`, `AutomergeBlockSpan`, `AutomergeMarkInfo`; Add C# wrappers: `GetMarks(obj)`, `GetMarksAtHeads(obj, heads)`, `GetSpans(obj)`, `GetSpansAtHeads(obj, heads)` — convert FFI `AutomergeSpan` to C# discriminated union
- [ ] Add unit tests: marks on text with single/multiple marks, marks_at historical, spans with text-only, spans with blocks+text, spans with marks on text sections, spans_at historical, verify mark values survive sync between two documents

## Sprint 6: Rich Text — Block Markers & UpdateSpans

- [ ] Add Rust FFI function in `doc.rs`: `split_block(obj, index)` → `AutomergeObjId` — call `self.inner.split_block(obj_id, index)` returning the new block map's ObjId (properties set via existing `map_put_*`)
- [ ] Add Rust FFI function in `doc.rs`: `update_spans(obj, spans)` — convert `ffi::Slice<AutomergeSpan>` to `Vec<automerge::Span>` and call `self.inner.update_spans(obj_id, span_vec)`. For text spans: reconstruct text content and mark set. For block spans: reconstruct block marker from type/parents/attrs
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# wrappers in `AutomergeDocument.cs`: `SplitBlock(obj, index)` returns `AutomergeObjectId`, `JoinBlock(obj, index)` implemented as `ListRemove(obj, index)` at block position, `UpdateSpans(obj, spans)` accepting `IReadOnlyList<AutomergeSpan>`
- [ ] Add unit tests: split_block creates block marker, set block properties via PutString on returned ObjId, join_block removes block, update_spans minimal diff, update_spans with block insertion, update_spans with text changes, spans round-trip (read → modify → update → read)
- [ ] Add integration test: full rich text workflow — create text, add block markers, apply marks, sync between two documents, verify spans are identical after sync

## Sprint 7: P1 — List Mutation & Cursor APIs

- [ ] Add Rust FFI functions in `doc.rs`: `list_splice(obj, idx, delete, values)` accepting `ffi::Slice<AutomergeScalarValue>` — call `self.inner.splice(obj_id, idx, delete, values_iter)` converting each `AutomergeScalarValue` to `ScalarValue`
- [ ] Add Rust FFI functions in `doc.rs`: `list_put_string`, `list_put_bool`, `list_put_int`, `list_put_uint`, `list_put_f64`, `list_put_bytes`, `list_put_null`, `list_put_object` — call `self.inner.put(obj_id, index, value)` for each type
- [ ] Add `AutomergeCursor` type to `types.rs` (opaque `ByteArray`); Add Rust FFI functions in `doc.rs`: `get_cursor(obj, idx)` and `get_cursor_position(obj, cursor)` — call `self.inner.get_cursor()` / `self.inner.get_cursor_position()`
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# types: `AutomergeCursor` class in `libs/Six.Automerge/src/AutomergeCursor.cs`; Add C# wrappers: `ListSplice()`, `ListPutString()`, `ListPutInt64()`, etc., `GetCursor()`, `GetCursorPosition()`
- [ ] Add unit tests: list_splice insert multiple, list_splice delete+insert, list_put replace element, cursor create and resolve, cursor survives concurrent insert before position, cursor survives concurrent insert after position, cursor persistence (encode/decode bytes)

## Sprint 8: P1 — Historical Queries, Iteration & Object Graph

- [ ] Add Rust FFI functions in `doc.rs`: `keys_at(obj, heads)` → `Vec<String>` and `length_at(obj, heads)` → `u64` — call `self.inner.keys_at()` / `self.inner.length_at()` from `ReadDoc`
- [ ] Add `AutomergePathElement` type to `types.rs`; Add Rust FFI functions in `doc.rs`: `values(obj)` and `values_at(obj, heads)` → `Vec<AutomergeValue>` — iterate `self.inner.values()` converting each `(Value, ExId)` pair
- [ ] Add Rust FFI functions in `doc.rs`: `map_range(obj, start_key, end_key)` and `list_range(obj, start_idx, end_idx)` → `Vec<AutomergeValue>` — call `self.inner.map_range()` / `self.inner.list_range()`
- [ ] Add Rust FFI functions in `doc.rs`: `parents(obj)` and `path_to_object(obj)` → `Vec<AutomergePathElement>` — call `self.inner.parents()` and collect path elements
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# types: `AutomergePathElement`, `AutomergeMapPathElement`, `AutomergeListPathElement` in `libs/Six.Automerge/src/`; Add C# wrappers: `GetKeysAtHeads()`, `LengthAtHeads()`, `Values()`, `ValuesAtHeads()`, `MapRange()`, `ListRange()`, `Parents()`, `PathToObject()`
- [ ] Add unit tests: keys_at at different historical points, length_at for maps and lists, values iteration for maps/lists, map_range subset query, list_range subset query, parents for deeply nested object, path_to_object round-trip

## Sprint 9: P1 — Save/Load Variants, Sync Extensions & Change Management

- [ ] Add `AutomergeSaveOptions` type to `types.rs`; Add Rust FFI functions in `doc.rs`: `save_nocompress()` → `ByteArray`, `save_with_options(opts)` → `ByteArray` — call `self.inner.save_nocompress()` / `self.inner.save_with_options()`
- [ ] Add Rust FFI function in `doc.rs`: `load_with_options(bytes, opts)` → `Result<Self>` — call `automerge::AutoCommit::load_with_options()` with verification mode config
- [ ] Add Rust FFI functions in `doc.rs`: `get_changes_added(other)` → `Vec<ChangeBytes>` — call `self.inner.get_changes_added(&other.inner)`; `bundle(hashes)` → `ByteArray` — call `self.inner.bundle(hashes_iter)`
- [ ] Add Rust FFI function in `doc.rs`: `diff_incremental()` — maintain `last_diff_heads: Option<Vec<ChangeHash>>` field on `AutomergeDoc` struct, call `self.inner.diff(&last_heads, &current_heads)`, update stored heads
- [ ] Add Rust FFI functions: `get_missing_deps(heads)` in `doc.rs` calling `self.inner.get_missing_deps(&heads)`; `has_our_changes(state)` in `doc.rs` calling `self.inner.has_our_changes(&state.inner)` — these are on the `ReadDoc` trait
- [ ] Regenerate `Interop.g.cs` and verify it compiles
- [ ] Add C# wrappers: `SaveNoCompress()`, `SaveWithOptions(options)`, `LoadWithOptions(bytes, options)`, `GetChangesAdded(other)`, `Bundle(heads)`, `DiffIncremental()`, `GetMissingDeps(heads)`, `HasOurChanges(syncState)` — plus `SaveOptions` C# type
- [ ] Add unit tests: save_nocompress produces valid document (load it back), save_with_options with/without compression, load_with_options, get_changes_added between diverged docs, bundle creates valid binary, diff_incremental tracks state across multiple commits, get_missing_deps for partially synced docs, has_our_changes before/after sync

## Sprint 10: Integration Testing & Final Verification

- [ ] Rich text end-to-end integration test: create doc → insert text → add block markers → apply marks (bold, italic, links) → splice text within marked regions → sync to second doc → verify spans match exactly
- [ ] Cross-document merge with rich text: two docs mark overlapping regions with different marks → merge → verify both marks are preserved without conflict
- [ ] Cursor persistence test: create cursor → save document → load document → resolve cursor → verify position is correct after concurrent edits by third party
- [ ] Full API surface verification: write a test that exercises every single new API method added across all sprints, ensuring no method throws `NotImplementedException` or unexpected errors
- [ ] Update `API_SURFACE_COVERAGE.md`: change all `❌` to `✅` for newly wrapped APIs, update statistics to show 100% coverage, add test counts for new test files
- [ ] Run complete test suite (`dotnet test` from repo root for all test projects), verify zero regressions in existing 448 tests, document final total test count
