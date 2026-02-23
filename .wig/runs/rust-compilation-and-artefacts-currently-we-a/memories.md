# 2026-02-21 15:29:36 UTC | Spec: rust-compilation-and-artefacts-currently-we-a | Task: sprint-1-task-5 | Sprint: Core Build Scripts & Rust Configuration

**Description**: Test `build-native.cs` locally: run for current platform (no flags), verify artifacts appear in `artefacts-rust/{os}-{arch}/` with correct naming. Run a second time with no changes and verify it skips compilation (incremental check). Run with `--force` and verify it recompiles. Run with `--run-codegen` and verify `generated/Interop.g.cs` is regenerated.

## Build Scripts (Sprint 1 - refactor/native-compile)
- `scripts/build-native.cs` — new incremental Rust compilation script with platform-aware output to `artefacts-rust/{os}-{arch}/`
- `scripts/copy-native-libs.cs` — copies artifacts from `artefacts-rust/` to output dir, strips platform suffix
- Both scripts use `Spectre.Console@0.49.1` for formatted output; escape `[` with `[[` in MarkupLine when literal brackets needed
- `build-info-ffi.json` stored in `libs/Six.Automerge.FFI/native/` for incremental build tracking
- Rust target dirs now use `target-{os}-{arch}` format (e.g. `target-linux-x64`) instead of old `target-linux`
- `.gitignore` updated with new `target-{os}-{arch}/` patterns and `artefacts-rust/*/` exclusion
- Old `build-six-automerge-ffi.cs` still used by MSBuild (Six.Automerge.FFI.csproj) — will need migration in later sprint
- Spectre.Console MarkupLine parses `[text]` as styles — use `[[text]]` to escape literal brackets

# 2026-02-21 15:48:03 UTC | Spec: rust-compilation-and-artefacts-currently-we-a | Task: sprint-2-task-5 | Sprint: MSBuild Integration & Project Cleanup

**Description**: Run the full test suite and verify all 1,028 tests pass: `dotnet tests-run.cs` from repo root (or from `scripts/` directory). Fix any test failures caused by the refactoring.

## Sprint 2 Learnings (MSBuild Integration)
- `tests-run.cs` script had pre-existing syntax bug: `#:property LangVersion 13` → needs `=` sign: `#:property LangVersion=14`
- Server.Api.Tests.Unit shows 0 tests in the test runner script but passes all 77 when run directly — likely script output parsing issue
- SignalR integration tests are flaky in batch runs but pass individually
- After removing CopyNativeDependencies from consuming projects, native libs are correctly propagated through project references via the FFI project's CopyNativeToOutput target
- Total test count is now 1,033 (up from documented 1,028)
- The `copy-native-libs.cs` script creates both direct output (`libsix_automerge.so`) and RID-specific (`runtimes/linux-x64/native/libsix_automerge.so`) in the output directory

# 2026-02-21 16:01:53 UTC | Spec: rust-compilation-and-artefacts-currently-we-a | Task: sprint-3-task-4 | Sprint: Publish Script & Pack Updates

**Description**: Test the pack workflow locally: run `dotnet scripts/pack.cs -- --local` and verify `.nupkg` files are created with native libs in `runtimes/{rid}/native/` paths. Inspect the package contents to confirm the RID structure. If only the current platform's artifact exists, verify the package contains only that platform's native lib (expected behavior for local dev).

## Sprint 3 Learnings (Publish Script & Pack Updates)
- `scripts/pack.cs` had old `#:property LangVersion 13` syntax (needs `=`), same as `runner.cs`
- `libs/Directory.Build.props` had `PackageIcon` using `$(MSBuildThisFileDirectory)\project_icon.png` which resolves to an absolute path — NuGet expects a package-relative path like `project_icon.png`
- `runner.cs` was referencing `scripts/nuget-pack.cs` (old name) — actual file is `scripts/pack.cs`
- `runner.cs` was referencing `scripts/test-runner.cs` (old name) — actual file is `scripts/runner.cs`
- Pack.cs CopyPackagesToLocalFeedAsync has pre-existing bug: glob `$"{pack.ProjectName}*.nupkg"` matches too broadly when project names are prefixes of others (e.g. `Six.Automerge` matches `Six.Automerge.FFI.*`)
- FFI NuGet package correctly includes native lib at `runtimes/{rid}/native/` via MSBuild ItemGroup conditions in Six.Automerge.FFI.csproj
- `--pack-only` mode in pack.cs preserves the `packages/` directory so user can inspect contents

# 2026-02-21 16:06:10 UTC | Spec: rust-compilation-and-artefacts-currently-we-a | Task: sprint-4-task-6 | Sprint: GitHub Actions Workflow

**Description**: Add `release` job (needs: `[build-native, verify-codegen, test]`). Runs on `ubuntu-latest`. Steps: checkout, setup .NET, download all 6 `native-*` artifacts — use `actions/download-artifact@v4` with pattern `native-*` and merge-multiple, then restructure downloaded directories so each platform's files are under `artefacts-rust/{os}-{arch}/`. Run `dotnet scripts/publish.cs -- --version ${{ inputs.version }} --feed ${{ inputs.feed }} --skip-native-build --skip-tests ${{ inputs.create_release && '--create-release' || '' }}`. Set env vars: `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` plus NuGet feed secrets (`NUGET_FEED_*` vars from repository secrets, conditionally used based on feed input).

Sprint 4 created `.github/workflows/build-test-publish.yml` with workflow_dispatch trigger. Uses `dtolnay/rust-toolchain@stable` for Rust setup. Native artifacts uploaded per-platform as `native-{os}-{arch}`, downloaded with `pattern: native-*` and `merge-multiple: true`. The restructure step handles flat merged files by parsing platform from artifact filenames (`libsix_automerge-{os}-{arch}.{ext}`). Linux ARM64 cross-compilation needs `gcc-aarch64-linux-gnu`. DOTNET_VERSION is `10.0.100-preview.6`.

