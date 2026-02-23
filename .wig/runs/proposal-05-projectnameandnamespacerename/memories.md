# 2026-02-21 14:18:48 UTC | Spec: proposal-05-projectnameandnamespacerename | Task: sprint-1-task-7 | Sprint: Rust Crate & Build Infrastructure Rename

**Description**: Clean all cargo target directories: delete `libs/Six.Automerge.FFI/native/target*/`, `libs/Six.Automerge.FFI/codegen/target*/`, and `libs/Six.Automerge.FFI/native/target/`, `libs/Six.Automerge.FFI/codegen/target/` — these contain stale `ray_automerge` fingerprints. Also delete `libs/Six.Automerge.FFI/build-info.json` to force full rebuild

Sprint 1 (Rust crate rename) established the pattern: native lib now uses `lib` prefix on ALL platforms including Windows. Cargo produces `six_automerge.dll` on Windows, build script renames it to `libsix_automerge.dll`. The codegen dll_name is `libsix_automerge` (matches the P/Invoke DllImport string that will be generated).

# 2026-02-21 14:34:28 UTC | Spec: proposal-05-projectnameandnamespacerename | Task: sprint-2-task-7 | Sprint: C# Namespace Mass Rename & Rebuild

**Description**: Run all unit tests (`dotnet test tests/Six.Automerge.FFI.Tests.Unit`, `dotnet test tests/Six.Automerge.Repo.Tests.Unit`, `dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit`) and verify pass counts match baseline (145 + 259 + 18 = 422 total, with 1 known pre-existing failure)

Sprint 2 namespace rename revealed that `.csproj` files also need updating — not just `.cs` files. Many test and app projects have `CopyNativeDependencies` MSBuild targets with hardcoded native lib paths and names (`Six.Automerge.FFI`, `ray_automerge.dll`, `libray_automerge.so`). The `sed` command targeting only `.cs` files missed these. Also discovered that Aspire project reference identifiers like `Projects.Ray_SignalR_TestServer` and `Projects.Ray_Tests_AppHost` use underscores instead of dots and need manual updating. When renaming across a codebase, always check `.csproj`, `.axaml`, `.json`, and other non-`.cs` files too.

# 2026-02-21 14:45:03 UTC | Spec: proposal-05-projectnameandnamespacerename | Task: sprint-4-task-6 | Sprint: Final Verification & Cleanup

**Description**: Update memory files: update `/home/dinko/.claude/projects/-mnt-d-Work-projects-RayX-RayX-Automerge/memory/MEMORY.md` to reflect the completed rename (remove Ray references, update project structure notes)

Sprint 4 (Final Verification & Cleanup) completed the Ray→Six rename. All 1,028 tests pass. The pre-existing DocumentIdTests.ToBinary_WithInvalidChecksum_ShouldThrow failure is no longer present (was likely fixed during the rename sprints). The Infra.AppHost requires Aspire workload to build on first run but works on second build from cache.

