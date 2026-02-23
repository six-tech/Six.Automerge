# Requirements: Rust Compilation and Artefacts Refactoring

## Problem Statement

The current Rust build pipeline for `Six.Automerge.FFI` has several issues:

1. **Broken artifact copying on Windows**: After the Ray-to-Six rename, native `.dll` files are not being copied to consuming projects' output directories, causing runtime crashes ("missing .dll").
2. **Fragile artifact distribution**: 14 consuming projects (apps + tests) each have duplicate `CopyNativeDependencies` MSBuild targets with hardcoded relative paths to `libs/Six.Automerge.FFI/bin/$(Configuration)/**`.
3. **No centralized artifact storage**: Native libraries land in Cargo's `target-{platform}/release/` then get copied to `$(TargetDir)`. There is no canonical artifact directory.
4. **No cross-compilation support**: The build script only compiles for the host platform. No CI matrix builds for multi-platform NuGet packaging.
5. **No GitHub release workflow**: The only workflow (`pr-test.yml`) runs on `ubuntu-latest` and only compiles for Linux.
6. **No publish/release automation**: No script to tag, create GitHub releases, or push NuGet packages as a coordinated operation.

## Functional Requirements

### FR1: Incremental Native Compilation Script (`build-native.cs`)
- Replace `build-six-automerge-ffi.cs` with a new script modeled on the WebView project's `build-native.cs`.
- Support explicit `--target-os` and `--target-arch` flags for cross-compilation.
- Auto-detect host platform when no flags provided (local development).
- Maintain checksum-based incremental build caching (SHA256 of all `.rs` files + `Cargo.toml`).
- Store build metadata in `build-info-ffi.json` (per-platform).
- Output compiled artifacts to `artefacts-rust/{os}-{arch}/` at repo root.
- Artifact naming: `libsix_automerge-{os}-{arch}.{ext}` (e.g., `libsix_automerge-windows-x64.dll`).
- Apply release optimizations: LTO, single codegen unit, symbol stripping, opt-level 3, panic=abort.
- Handle Interoptopus codegen as a separate step (only when `--run-codegen` flag is passed or when codegen sources changed).
- Support `--force` flag to skip incremental checks.
- Support `--verbose` flag for detailed output.

### FR2: Native Library Deployment Script (`copy-native-libs.cs`)
- Create a new script modeled on the WebView project's `copy-native-libs.cs`.
- Copy from `artefacts-rust/{os}-{arch}/` to a specified output directory.
- Strip the platform suffix when copying: `libsix_automerge-{os}-{arch}.dll` -> `libsix_automerge.dll`.
- Also copy to `runtimes/{rid}/native/` subdirectory for .NET RID-based probing.
- Platform-to-RID mapping:
  - `windows-x64` -> `win-x64`
  - `windows-arm64` -> `win-arm64`
  - `linux-x64` -> `linux-x64`
  - `linux-arm64` -> `linux-arm64`
  - `macos-x64` -> `osx-x64`
  - `macos-arm64` -> `osx-arm64`
- Auto-detect current platform when no `--target-os`/`--target-arch` provided.

### FR3: Centralized Artifact Storage
- All native artifacts stored in `artefacts-rust/` at repo root.
- Structure: `artefacts-rust/{os}-{arch}/libsix_automerge-{os}-{arch}.{ext}`
- Supported platforms:
  - `windows-x64/` (`.dll`, `.pdb`)
  - `windows-arm64/` (`.dll`, `.pdb`)
  - `linux-x64/` (`.so`)
  - `linux-arm64/` (`.so`)
  - `macos-x64/` (`.dylib`)
  - `macos-arm64/` (`.dylib`)
- `artefacts-rust/` contents are git-ignored (artifacts are build outputs, not source).
- A `.gitkeep` or `.gitignore` file keeps the directory in version control.

### FR4: Simplified MSBuild Integration
- `Six.Automerge.FFI.csproj` calls `build-native.cs` (incremental) then `copy-native-libs.cs` during build.
- A `SkipRustBuild` MSBuild property allows CI to bypass Rust compilation when artifacts are pre-built.
- Native libs included in NuGet package under `runtimes/{rid}/native/` paths using conditional `Exists()` checks.
- Remove all 14 `CopyNativeDependencies` targets from consuming projects — native libs flow through project reference output copying or the `copy-native-libs.cs` script invoked from FFI.csproj.

### FR5: Single NuGet Package with Multi-Platform RIDs
- `Six.Automerge.FFI` NuGet package includes native libraries for all available platforms.
- Package layout:
  ```
  runtimes/win-x64/native/libsix_automerge.dll
  runtimes/win-arm64/native/libsix_automerge.dll
  runtimes/linux-x64/native/libsix_automerge.so
  runtimes/linux-arm64/native/libsix_automerge.so
  runtimes/osx-x64/native/libsix_automerge.dylib
  runtimes/osx-arm64/native/libsix_automerge.dylib
  ```
- Conditional inclusion: only artifacts present in `artefacts-rust/` are included.
- During local development, only the host platform's artifact is in the package (sufficient for local testing).
- During CI release, all 6 platform artifacts are present -> full multi-platform package.
- .NET's built-in RID-based native library probing handles loading the correct library at runtime.

### FR6: Publish Script (`publish.cs`)
- New script in `scripts/` that orchestrates the full release pipeline.
- CLI interface:
  - `--version <semver>`: Version to tag and pack (e.g., `1.0.0-alpha.2`)
  - `--feed <target>`: NuGet target — `pack-only`, `local`, `dev`, `staging`, `prod` (default: `pack-only`)
  - `--skip-tests`: Skip test execution
  - `--skip-native-build`: Skip Rust compilation (artifacts must already exist)
  - `--create-release`: Create GitHub release using `gh` CLI
  - `--dry-run`: Show what would happen without executing
- Pipeline steps:
  1. Build native for current platform (unless `--skip-native-build`)
  2. Build .NET solution (`dotnet build --configuration Release`)
  3. Run tests (`dotnet test`) unless `--skip-tests`
  4. Pack NuGet packages (`dotnet pack`)
  5. Create git tag (`v{version}`)
  6. Create GitHub release with `gh release create` (if `--create-release`), attach native artifacts from `artefacts-rust/`
  7. Push NuGet to specified feed (if feed != `pack-only`)
- Uses `CliWrap` and `Spectre.Console` for process execution and UI (consistent with existing scripts).

### FR7: GitHub Actions Workflow (`build-test-publish.yml`)
- Manual dispatch (`workflow_dispatch`) with inputs:
  - `version`: Semantic version string (required)
  - `feed`: NuGet target — `pack-only`, `local`, `dev`, `staging`, `prod` (default: `pack-only`)
  - `create_release`: Boolean — create GitHub release (default: `true`)
- Jobs:
  1. **`build-native`**: Matrix strategy across 6 platform targets. Each runner compiles Rust for its target and uploads artifacts.
  2. **`verify-codegen`**: Single runner (Linux x64) runs codegen and diffs `Interop.g.cs` against committed version. Fails if different.
  3. **`test`**: Downloads Linux x64 artifacts, copies to `artefacts-rust/`, runs `dotnet test`.
  4. **`release`**: Downloads ALL platform artifacts, runs `dotnet scripts/publish.cs` with `--skip-native-build --skip-tests --create-release --version <input> --feed <input>`.
- Matrix targets and runners:
  | Target | Runner |
  |--------|--------|
  | windows-x64 | `windows-latest` |
  | windows-arm64 | `windows-latest` (cross-compile) |
  | linux-x64 | `ubuntu-latest` |
  | linux-arm64 | `ubuntu-latest` (cross-compile with `aarch64-unknown-linux-gnu` target) |
  | macos-x64 | `macos-13` (Intel runner) |
  | macos-arm64 | `macos-14` (ARM runner) |

### FR8: Codegen Verification
- Interoptopus codegen generates platform-independent C# code (`Interop.g.cs`).
- The generated file remains committed to the repository (as it is today).
- CI verifies the committed file matches a fresh codegen run.
- Developers run codegen locally when modifying Rust FFI functions and commit the result.

### FR9: Delete Legacy Workflow
- Remove `.github/workflows/pr-test.yml`.
- The new `build-test-publish.yml` replaces it.

## Non-Functional Requirements

### NFR1: Build Performance
- Incremental builds with no Rust changes complete in <5 seconds.
- Full Rust compilation for a single platform completes in <3 minutes.
- CI pipeline (all 6 platforms + tests + pack) completes in <20 minutes.

### NFR2: Backwards Compatibility
- `dotnet build` from repo root continues to work seamlessly for local development.
- All 1,028 existing tests continue to pass without modification.
- P/Invoke library name (`libsix_automerge`) remains unchanged.

### NFR3: Security
- No secrets, API keys, or PATs committed to the repository.
- NuGet feed credentials passed via environment variables.
- GitHub release creation uses `GITHUB_TOKEN` (auto-provided in Actions).

### NFR4: Maintainability
- Build logic centralized in `scripts/` — minimal logic in `.csproj` files.
- Consistent patterns with the WebView project (`RayX.Avalonia.Controls.WebView`).
- Rust toolchain pinned via `rust-toolchain.toml`.

### NFR5: Cross-Platform Developer Experience
- Windows, Linux, and macOS developers can `dotnet build` with no extra setup.
- Rust toolchain auto-installs targets specified in `rust-toolchain.toml`.
- Cross-compilation requires only standard toolchain components (no exotic dependencies for local dev).

## Scope Exclusions

- Android, iOS, WebAssembly, and other mobile/embedded targets are out of scope.
- Per-platform NuGet package splitting (separate `.Windows`, `.Linux`, `.macOS` packages) is not needed — a single package with all RIDs is used.
- Automated version bumping or changelog generation.
- Integration test infrastructure changes (Aspire, Docker).
