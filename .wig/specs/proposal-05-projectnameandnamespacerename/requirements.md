# Requirements: Project Name and Namespace Rename (Ray -> Six)

## Background

The project/directory/csproj rename from `Six.Automerge.*` to `Six.Automerge.*` was partially completed in a prior commit (`ecfcb43`). Project files, directories, solution file, and `RootNamespace` settings are already `Six.*`. However, the actual source code (C# namespaces, Rust crate names, build scripts, documentation, and configuration) still uses `Ray.*` naming throughout.

## Functional Requirements

### FR-1: C# Namespace Rename
- All `namespace Six.Automerge.*` declarations must become `namespace Six.Automerge.*`
- All `using Six.Automerge.*` directives must become `using Six.Automerge.*`
- All `Ray.SignalR.*` namespaces must become `Six.SignalR.*`
- All `Six.Automerge.*` string references in code (fully-qualified type names, comments) must become `Six.Automerge.*`
- The rename must be a direct prefix swap: `Ray.` -> `Six.`, preserving the rest of the namespace path unchanged

### FR-2: Rust Crate Rename
- Native crate name: `ray_automerge` -> `six_automerge`
- Codegen crate name: `ray_automerge_codegen` -> `six_automerge_codegen`
- All internal Rust references updated accordingly
- Regenerated `Interop.g.cs` must use `Six.Automerge.FFI.Native` namespace

### FR-3: Native Artifact Naming Unification
- All platforms use `lib` prefix for consistency:
  - Windows: `libsix_automerge.dll` (previously `ray_automerge.dll`, no lib prefix)
  - Linux: `libsix_automerge.so` (previously `libray_automerge.so`)
  - macOS: `libsix_automerge.dylib` (previously `libray_automerge.dylib`)
- DllImport name in generated code must be `libsix_automerge`

### FR-4: Build Script Rename
- `scripts/build-ray-automerge-ffi.cs` -> `scripts/build-six-automerge-ffi.cs`
- All internal references (library name, crate name, artifact names) updated
- FFI `.csproj` must reference the renamed script path

### FR-5: Documentation Update
- All `docs/src/content/docs/` MDX files must use `Six.Automerge.*` namespaces
- Docs site config (`astro.config.mjs`) must reference Six naming and URLs
- `README.md`, `CLAUDE.md`, `BUILD.md` must use Six naming throughout
- Other markdown files (`PROPOSAL-*`, `API_SURFACE_COVERAGE.md`) updated

### FR-6: Configuration & Metadata Update
- `.wig/` memory and spec files updated
- Any remaining references to `Six.Automerge` updated to `Six.Automerge`

## Non-Functional Requirements

### NFR-1: Scripted Approach
- Use automated find-and-replace scripts rather than manual file-by-file editing
- Scripts must be precise to avoid false positives (e.g., should not touch `.references/` directory or `node_modules/`)

### NFR-2: Build Integrity
- After rename, `dotnet build` from repo root must succeed
- All existing tests must pass (same count as before rename)
- Rust `cargo build --release` must succeed for both native and codegen crates

### NFR-3: No Behavioral Changes
- This is a pure rename refactor with zero functional/behavioral changes
- All APIs, types, methods, properties remain identical except for namespace prefix
- Test behavior and counts must be identical pre/post rename

### NFR-4: Clean Cargo Targets
- All platform-specific cargo target directories (`target-windows/`, `target-linux/`, etc.) must be cleaned
- Stale fingerprints referencing `ray_automerge` must not remain

### NFR-5: Scope Exclusions
- `.references/` directory (third-party source) must NOT be modified
- `node_modules/` must NOT be modified
- `docs/.astro/data-store.json` will regenerate automatically on next docs build
