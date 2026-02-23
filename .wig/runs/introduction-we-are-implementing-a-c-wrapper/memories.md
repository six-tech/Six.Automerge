# 2026-02-19 23:30:06 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-1-task-5 | Sprint: Build Pipeline Verification & FileSystem Review

**Description**: Run FileSystem storage tests (`dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit` and `dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Integration`) and document any issues

Pre-existing failing test: DocumentIdTests.ToBinary_WithInvalidChecksum_ShouldThrow in Six.Automerge.Repo.Tests.Unit. The test corrupts the last char with 'X' (valid Base58) expecting FormatException, but Base58Check.Decode doesn't throw for this corruption. The TryParse method has a blanket catch that hides decode failures, but if the checksum happens to pass or the library doesn't validate checksums, no exception occurs. Total test counts: FFI=145, Repo=260 (259 pass, 1 fail).

# 2026-02-19 23:30:06 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-1-task-5 | Sprint: Build Pipeline Verification & FileSystem Review

**Description**: Run FileSystem storage tests (`dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit` and `dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Integration`) and document any issues

FileSystemStorageAdapter has a critical deadlock bug in LoadRange: it holds SemaphoreSlim(1,1) and calls Load() which also tries to acquire the same non-re-entrant semaphore. Also has a RemoveRange cache staleness issue — doesn't clear matching cache entries after directory deletion.

# 2026-02-19 23:40:55 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-2-task-7 | Sprint: P0 — Counter Increment, Text Read, Fork

**Description**: Add unit tests in `tests/Six.Automerge.FFI.Tests.Unit/src/`: counter increment (map + list, positive + negative), text read (empty, non-empty, after splice), text_at historical read, fork at current heads, empty_change, base_heads

Interoptopus forbids parameter name `value` — use `delta` or another name instead. Codegen panics with "has a forbidden name that might cause issues in other languages" error.

# 2026-02-19 23:51:06 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-3-task-7 | Sprint: P0 — List/Map Get, Object Access, Historical & Conflict Reads

**Description**: Add unit tests: list_get for all types, list_get_object for nested containers, map_get_object, get_at historical reads for maps and lists, get_all conflict detection with 2+ concurrent writers

Sprint 3 test count: FFI.Tests.Unit = 198 pass (145 baseline + 20 sprint 2 + 33 sprint 3). AutomergeConflictValue in types.rs needs `from_value_and_id` helper that takes `&Value` and `&ObjId`. For get_all, Automerge returns `Vec<(Value, ObjId)>` where ObjId represents the operation ID (ExId). The `AutomergeScalarValue::new()` is crate-private (not pub) in types.rs so it can be accessed from within types.rs. Inventory.rs needs `builtins_vec!` registration for any new FFI Vec types.

# 2026-02-20 00:03:04 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-4-task-6 | Sprint: Rich Text — Marks Write API

**Description**: Add unit tests: mark bold on text range, unmark bold, mark with each expand mode, mark with different value types (string, bool, int), overlapping marks, mark on empty text (error), mark with invalid range (error)

Sprint 4 test count: FFI.Tests.Unit = 219 pass (198 baseline + 21 sprint 4 marks). AutomergeScalarValue can be passed from C# to Rust FFI as input — construct with typed helper methods (CreateStringScalar, CreateBoolScalar, CreateIntScalar) setting value_type and the corresponding field, with empty VecU8 for unused byte fields. The `to_scalar_value()` reverse conversion on Rust side converts back to `ScalarValue`. Automerge mark/unmark: `start > end` is silently accepted as no-op (no exception). The parameter name `value` is forbidden by Interoptopus — use `mark_value` or similar. ExpandMark default is `After`.

# 2026-02-20 00:17:18 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-5-task-7 | Sprint: Rich Text — Marks & Spans Read API

**Description**: Add unit tests: marks on text with single/multiple marks, marks_at historical, spans with text-only, spans with blocks+text, spans with marks on text sections, spans_at historical, verify mark values survive sync between two documents

Sprint 5 completed: Marks & Spans Read API. Test count now 240 (was 219). New files: AutomergeMarkInfo.cs, AutomergeSpan.cs. New Rust FFI: marks(), marks_at(), spans(), spans_at(). Key pattern: `from_mark_entry` on AutomergeMarkData needed to be made `pub` for cross-module access in doc.rs. Block spans use hydrate_value_to_json() for arbitrary attributes.

# 2026-02-20 00:47:10 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-7-task-6 | Sprint: P1 — List Mutation & Cursor APIs

**Description**: Add unit tests: list_splice insert multiple, list_splice delete+insert, list_put replace element, cursor create and resolve, cursor survives concurrent insert before position, cursor survives concurrent insert after position, cursor persistence (encode/decode bytes)

Sprint 7 test count: FFI.Tests.Unit = 275 pass (253 baseline + 22 sprint 7 list mutation + cursor). Key patterns: `list_put_*` functions use `self.inner.put(obj_id, index as usize, value)` with usize index for replacing list elements at position. `list_splice` takes `ffi::Slice<AutomergeScalarValue>` and converts each via `to_scalar_value()`. Cursor is represented as `ByteArray` on FFI boundary — `Cursor::to_bytes()` and `Cursor::try_from(&[u8])` for serialization. The `get_cursor` and `get_cursor_position` both take `None` for the `at` parameter (current state). `AutomergeScalarValue` constructor is `internal` — tests need `InternalsVisibleTo` (added `Six.Automerge.FFI.Tests.Unit` to csproj). Repo tests: 260 pass.

# 2026-02-20 01:03:57 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-8-task-7 | Sprint: P1 — Historical Queries, Iteration & Object Graph

**Description**: Add unit tests: keys_at at different historical points, length_at for maps and lists, values iteration for maps/lists, map_range subset query, list_range subset query, parents for deeply nested object, path_to_object round-trip

Sprint 8 test count: FFI.Tests.Unit = 296 pass (275 baseline + 21 sprint 8 historical queries). New Rust FFI functions: `map_keys_at`, `length_at`, `obj_values`, `obj_values_at`, `obj_map_range`, `obj_map_range_full`, `obj_list_range`, `obj_list_range_full`, `obj_parents`, `obj_path_to_object`. Key patterns: `automerge::ObjId` is actually `ExId` re-exported (`pub use exid::{ExId as ObjId}`), so `Parents.path()` returns `Vec<(ExId, Prop)>` which is compatible with `AutomergeObjId::from(ObjId)`. The `MapRangeItem.value` is `ValueRef<'a>` which has `into_value()` (not `value()`) — must call `id()` before `into_value()` due to ownership. `AutomergeConflictValue` reused for range/values returns since it already packages value+opId. The Interoptopus parameter name `value` is forbidden — the `values()` FFI function was named `obj_values()` to avoid conflicts. Repo tests: 260 pass.

# 2026-02-20 01:18:53 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-9-task-8 | Sprint: P1 — Save/Load Variants, Sync Extensions & Change Management

**Description**: Add unit tests: save_nocompress produces valid document (load it back), save_with_options with/without compression, load_with_options, get_changes_added between diverged docs, bundle creates valid binary, diff_incremental tracks state across multiple commits, get_missing_deps for partially synced docs, has_our_changes before/after sync

Sprint 9 test count: FFI.Tests.Unit = 319 pass (296 baseline + 23 sprint 9 save/load/sync). New Rust FFI functions: `save_nocompress`, `save_with_options`, `load_with_options`, `diff_incremental`, `get_changes_added`, `bundle`, `get_missing_deps`, `has_our_changes`. Key findings: `diff_incremental()` exists natively on `AutoCommit` (no manual state tracking needed). `get_missing_deps(heads)` returns deps of `heads` that are NOT present in the document (with empty heads = empty result, not all changes). `has_our_changes(sync_state)` is on `AutoCommit` not `SyncDoc` trait. `LoadOptions` in automerge uses a builder pattern with `verification_mode()` and `on_partial_load()`. `SaveOptions` has `deflate: bool` and `retain_orphans: bool`. `bundle()` is experimental (format may change) and returns `Bundle` with `.bytes()` method. Interoptopus parameter name `value` is still forbidden. Repo tests: 260 pass.

# 2026-02-20 01:37:03 UTC | Spec: introduction-we-are-implementing-a-c-wrapper | Task: sprint-10-task-6 | Sprint: Integration Testing & Final Verification

**Description**: Run complete test suite (`dotnet test` from repo root for all test projects), verify zero regressions in existing 448 tests, document final total test count

Sprint 10 final test counts: FFI.Tests.Unit=379, Repo.Tests.Unit=260, Storage.FileSystem.Tests.Unit=18, Storage.FileSystem.Tests.Integration=9. Grand total: 666 tests, 0 failures. All P0 and P1 API gaps from the original coverage report are now filled. The pre-existing DocumentIdTests.ToBinary_WithInvalidChecksum_ShouldThrow test no longer fails in Repo.Tests.Unit (260 pass, 0 fail vs original 259 pass, 1 fail).

