# Implementation Plan: Project Name and Namespace Rename (Ray -> Six)

## Sprint 1: Rust Crate & Build Infrastructure Rename

Rename all Rust crate names, codegen configuration, and the build script. This sprint does NOT rebuild — Sprint 2 handles the full rebuild after C# namespaces are also renamed.

- [ ] Update `libs/Six.Automerge.FFI/native/Cargo.toml`: change `[package] name` from `ray_automerge` to `six_automerge` and `[lib] name` from `ray_automerge` to `six_automerge`
- [ ] Update `libs/Six.Automerge.FFI/codegen/Cargo.toml`: change package name from `ray_automerge_codegen` to `six_automerge_codegen`, and dependency `ray_automerge = { path = "../native" }` to `six_automerge = { path = "../native" }`
- [ ] Update `libs/Six.Automerge.FFI/codegen/src/main.rs`: change `ray_automerge::ffi_inventory()` to `six_automerge::ffi_inventory()`, namespace from `"Six.Automerge.FFI.Native"` to `"Six.Automerge.FFI.Native"`, and `.dll_name("ray_automerge")` to `.dll_name("libsix_automerge")`
- [ ] Update `libs/Six.Automerge.FFI/native/src/util.rs`: change version string from `"Six.Automerge FFI v{}"` to `"Six.Automerge FFI v{}"`
- [ ] Rename build script file from `scripts/build-ray-automerge-ffi.cs` to `scripts/build-six-automerge-ffi.cs` and update its contents: `LibraryName` to `"Six.Automerge.FFI"`, `NativeCrateName` to `"six_automerge"`, `GetNativeLibraryName` to produce `lib` prefix on all platforms (including Windows), and `CopyNativeOutputs` to handle the Windows rename from `six_automerge.dll` to `libsix_automerge.dll`
- [ ] Update `libs/Six.Automerge.FFI/Six.Automerge.FFI.csproj`: change `RustBuildScript` path to `../../scripts/build-six-automerge-ffi.cs`, and update ALL native artifact references in `<NativeOutputs>` and `<None>` items from `ray_automerge.dll`/`libray_automerge.so`/`libray_automerge.dylib` to `libsix_automerge.dll`/`libsix_automerge.so`/`libsix_automerge.dylib`
- [ ] Clean all cargo target directories: delete `libs/Six.Automerge.FFI/native/target*/`, `libs/Six.Automerge.FFI/codegen/target*/`, and `libs/Six.Automerge.FFI/native/target/`, `libs/Six.Automerge.FFI/codegen/target/` — these contain stale `ray_automerge` fingerprints. Also delete `libs/Six.Automerge.FFI/build-info.json` to force full rebuild

## Sprint 2: C# Namespace Mass Rename & Rebuild

Script-based mass rename of all C# namespaces and usings, then rebuild everything including Rust codegen to verify.

- [ ] Write and execute a shell script (using `sed` or `find ... -exec sed`) to replace ALL occurrences of `Six.Automerge` with `Six.Automerge` across every `.cs` file in `libs/`, `apps/`, `tests/` directories. Exclude `.references/`, `node_modules/`, `obj/`, `bin/` directories. This handles both `namespace` declarations and `using` directives in one pass
- [ ] Run a second pass to replace `Ray.SignalR` with `Six.SignalR` in all `.cs` files (affects `tests/Six.SignalR.TestServer/` and related test files)
- [ ] Check for and replace any remaining `Ray.Toolkit` references in `.cs` files (e.g., `Six.Automerge` -> `Six.Automerge`)
- [ ] Run `cargo build --release` in `libs/Six.Automerge.FFI/native/` to verify the Rust native crate builds with the new name
- [ ] Run `cargo run --release` in `libs/Six.Automerge.FFI/codegen/` to regenerate `libs/Six.Automerge.FFI/generated/Interop.g.cs` with `Six.Automerge.FFI.Native` namespace and `libsix_automerge` DllImport name
- [ ] Run `dotnet build` from the repo root to verify the entire solution compiles successfully
- [ ] Run all unit tests (`dotnet test tests/Six.Automerge.FFI.Tests.Unit`, `dotnet test tests/Six.Automerge.Repo.Tests.Unit`, `dotnet test tests/Six.Automerge.Repo.Storage.FileSystem.Tests.Unit`) and verify pass counts match baseline (145 + 259 + 18 = 422 total, with 1 known pre-existing failure)

## Sprint 3: Documentation & Configuration Updates

Update all documentation files, site config, and metadata to use Six naming.

- [ ] Write and execute a shell script to replace `Six.Automerge` with `Six.Automerge` and `Six.Automerge` with `Six.Automerge` across all `.mdx` and `.md` files in `docs/src/content/docs/`. Also replace `Six.Automerge.Repository` with `Six.Automerge.Repository` (used in docs API references)
- [ ] Update `docs/astro.config.mjs`: change `base` from `'/Six.Automerge/'` to `'/Six.Automerge/'`, `site` from `'https://ray-platform.github.io'` to `'https://six-tech.github.io'`, and `title` from `'Six.Automerge'` to `'Six.Automerge'`
- [ ] Update `README.md`: replace all `Six.Automerge` references with `Six.Automerge`, update project structure tree, update build commands, remove `Six.Automerge` references
- [ ] Update `CLAUDE.md`: replace all `Six.Automerge` and `Ray.` references with `Six.Automerge` and `Six.` respectively throughout the project overview, architecture diagram, build instructions, and code examples
- [ ] Update `libs/Six.Automerge.FFI/BUILD.md`: replace all `Six.Automerge` and `Six.Automerge` references with `Six.Automerge`
- [ ] Update remaining markdown files at repo root: `API_SURFACE_COVERAGE.md`, `PROPOSAL-01-API.md`, `PROPOSAL-04.md`, `PROPOSAL-05-ProjectNameAndNamespaceRename.md` — replace `Six.Automerge` with `Six.Automerge` throughout
- [ ] Update `.wig/` files: run a script to replace `Six.Automerge` with `Six.Automerge` across all `.md` and `.json` files in `.wig/specs/`, `.wig/runs/`, and `.wig/memory/` directories

## Sprint 4: Final Verification & Cleanup

Comprehensive verification that no `Ray` references remain, full build and test pass.

- [ ] Run a comprehensive `grep` scan for any remaining `ray_automerge`, `Six.Automerge`, `Ray.Toolkit`, `Ray.SignalR`, `Ray\.` across all source files (`.cs`, `.rs`, `.toml`, `.csproj`, `.props`, `.targets`, `.slnx`, `.md`, `.mdx`, `.mjs`, `.json`), excluding `.references/`, `node_modules/`, `obj/`, `bin/`, cargo `target*/` directories
- [ ] Fix any remaining references found by the scan
- [ ] Run `dotnet build` from repo root to verify clean build
- [ ] Run all unit test suites and confirm pass counts match pre-rename baseline
- [ ] Run all demo app test suites (`tests/DemoApp.*.Tests/`) to verify they compile and pass
- [ ] Update memory files: update `/home/dinko/.claude/projects/-mnt-d-Work-projects-RayX-RayX-Automerge/memory/MEMORY.md` to reflect the completed rename (remove Ray references, update project structure notes)
