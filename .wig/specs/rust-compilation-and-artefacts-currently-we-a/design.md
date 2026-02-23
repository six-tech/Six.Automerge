# Design: Rust Compilation and Artefacts Refactoring

## Architecture Overview

```
┌───────────────────────────────────────────────────────────────────┐
│  GitHub Actions: build-test-publish.yml (manual dispatch)         │
│  ┌──────────────────────────────────────────────────────────────┐ │
│  │ Matrix: build-native (6 platforms in parallel)               │ │
│  │  win-x64 │ win-arm64 │ linux-x64 │ linux-arm64 │ osx-x64/a │ │
│  └────────────────────────┬─────────────────────────────────────┘ │
│  ┌─────────────┐          │          ┌──────────────────────┐     │
│  │verify-codegen│          │          │ test (linux-x64)     │     │
│  └─────────────┘          ▼          └──────────────────────┘     │
│                   ┌───────────────┐                               │
│                   │ release job   │                               │
│                   │ publish.cs    │                               │
│                   └───────────────┘                               │
└───────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────┐
│  Local Development: dotnet build                                  │
│  ┌─────────────────────┐    ┌────────────────────┐               │
│  │ build-native.cs     │───▶│ artefacts-rust/    │               │
│  │ (incremental/auto)  │    │ {os}-{arch}/       │               │
│  └─────────────────────┘    └────────┬───────────┘               │
│                                      │                            │
│  ┌─────────────────────┐             │                            │
│  │ copy-native-libs.cs │◀────────────┘                            │
│  │ (MSBuild target)    │────▶ bin/{Config}/net10.0/               │
│  └─────────────────────┘                                          │
└───────────────────────────────────────────────────────────────────┘
```

## Directory Structure

### New Files/Directories

```
RayX.Automerge/
├── artefacts-rust/                          # NEW: centralized native artifacts
│   ├── .gitignore                           # Ignore all artifacts, keep directory
│   ├── windows-x64/
│   │   └── libsix_automerge-windows-x64.dll
│   ├── windows-arm64/
│   │   └── libsix_automerge-windows-arm64.dll
│   ├── linux-x64/
│   │   └── libsix_automerge-linux-x64.so
│   ├── linux-arm64/
│   │   └── libsix_automerge-linux-arm64.so
│   ├── macos-x64/
│   │   └── libsix_automerge-macos-x64.dylib
│   └── macos-arm64/
│       └── libsix_automerge-macos-arm64.dylib
├── scripts/
│   ├── build-native.cs                      # NEW: replaces build-six-automerge-ffi.cs
│   ├── copy-native-libs.cs                  # NEW: artifact deployment
│   ├── publish.cs                           # NEW: release orchestrator
│   ├── pack.cs                              # UPDATED: handles new artifact structure
│   ├── runner.cs                            # UPDATED: new menu entries
│   ├── tests-run.cs                         # UNCHANGED
│   ├── tests-coverage.cs                    # UNCHANGED
│   └── tests-report.cs                      # UNCHANGED
├── libs/Six.Automerge.FFI/
│   ├── native/
│   │   ├── .cargo/config.toml               # NEW: cross-compilation linker config
│   │   └── rust-toolchain.toml              # NEW: pinned Rust toolchain + targets
│   ├── Six.Automerge.FFI.csproj             # REFACTORED: new MSBuild targets
│   └── generated/Interop.g.cs               # UNCHANGED (committed, verified in CI)
├── .github/workflows/
│   ├── build-test-publish.yml               # NEW: comprehensive CI/CD workflow
│   └── deploy-gh-pages.yml                  # UNCHANGED
└── .gitignore                               # UPDATED: add artefacts-rust/ patterns
```

### Deleted Files

```
scripts/build-six-automerge-ffi.cs           # Replaced by build-native.cs
libs/Six.Automerge.FFI/build-info.json       # Replaced by build-info-ffi.json in native/
.github/workflows/pr-test.yml                # Replaced by build-test-publish.yml
```

## Script Designs

### 1. `build-native.cs` — Incremental Rust Compilation

**CLI Interface:**
```
dotnet scripts/build-native.cs -- [OPTIONS]

OPTIONS:
  --target-os <os>         Target OS: windows, linux, macos (default: auto-detect)
  --target-arch <arch>     Target architecture: x64, arm64 (default: auto-detect)
  --project <path>         Rust project directory (default: libs/Six.Automerge.FFI/native)
  --run-codegen            Also run Interoptopus codegen after native build
  --force, -f              Force rebuild regardless of checksums
  --verbose, -v            Verbose cargo output
```

**Core Logic:**
```
1. Resolve project directory (native crate root)
2. Determine target triple from --target-os + --target-arch:
   - windows-x64  -> x86_64-pc-windows-msvc
   - windows-arm64 -> aarch64-pc-windows-msvc
   - linux-x64    -> x86_64-unknown-linux-gnu
   - linux-arm64  -> aarch64-unknown-linux-gnu
   - macos-x64    -> x86_64-apple-darwin
   - macos-arm64  -> aarch64-apple-darwin
3. Check if cross-compiling (target != host)
4. Load build-info-ffi.json from native/ directory
5. Compute checksums of native/Cargo.toml + native/src/**/*.rs
6. Compare with stored checksums + platform + profile
7. If changed (or --force):
   a. Set cargo env vars: LTO=true, CODEGEN_UNITS=1, STRIP=symbols, OPT_LEVEL=3, PANIC=abort
   b. Run: cargo build --release --manifest-path native/Cargo.toml
          [--target <triple> if cross-compiling]
          --target-dir native/target-{os}
   c. Copy artifact from target dir to artefacts-rust/{os}-{arch}/
      - Rename: {cargo_output_name} -> libsix_automerge-{os}-{arch}.{ext}
   d. Copy debug symbols (.pdb) if present
   e. Save updated build-info-ffi.json
8. If --run-codegen:
   a. Compute checksums of codegen/Cargo.toml + codegen/src/**/*.rs
   b. If changed:
      - Run: cargo run --release --manifest-path codegen/Cargo.toml
             --target-dir codegen/target-{os}
      - Verify generated/Interop.g.cs exists
```

**Cargo Output Name Resolution:**
| OS | Cargo Produces | Artifact Name |
|----|---------------|---------------|
| windows | `six_automerge.dll` | `libsix_automerge-windows-{arch}.dll` |
| linux | `libsix_automerge.so` | `libsix_automerge-linux-{arch}.so` |
| macos | `libsix_automerge.dylib` | `libsix_automerge-macos-{arch}.dylib` |

**Build Info JSON Schema:**
```json
{
  "targetOs": "windows",
  "targetArch": "x64",
  "profile": "release",
  "rustVersion": "1.85.0",
  "lastBuildUtc": "2026-02-21T10:30:00Z",
  "checksums": {
    "native/Cargo.toml": "ABC123...",
    "native/src/lib.rs": "DEF456...",
    "native/src/doc.rs": "GHI789..."
  }
}
```

### 2. `copy-native-libs.cs` — Artifact Deployment

**CLI Interface:**
```
dotnet scripts/copy-native-libs.cs -- [OPTIONS]

OPTIONS:
  --target-os <os>         Target OS (default: auto-detect)
  --target-arch <arch>     Target architecture (default: auto-detect)
  --output-dir <path>      Output directory (required)
  --artefacts-dir <path>   Artefacts root (default: artefacts-rust/ at repo root)
```

**Core Logic:**
```
1. Resolve artefacts-rust/ directory
2. Determine target os+arch
3. Find source: artefacts-rust/{os}-{arch}/libsix_automerge-{os}-{arch}.{ext}
4. Determine dest library name: libsix_automerge.{ext}
5. Determine RID: windows-x64 -> win-x64, macos-arm64 -> osx-arm64, etc.
6. Copy to output-dir/libsix_automerge.{ext} (for immediate P/Invoke)
7. Copy to output-dir/runtimes/{rid}/native/libsix_automerge.{ext} (for .NET RID probing)
```

**Platform-to-RID Mapping:**
| Source Dir | RID | Extension |
|-----------|-----|-----------|
| `windows-x64` | `win-x64` | `.dll` |
| `windows-arm64` | `win-arm64` | `.dll` |
| `linux-x64` | `linux-x64` | `.so` |
| `linux-arm64` | `linux-arm64` | `.so` |
| `macos-x64` | `osx-x64` | `.dylib` |
| `macos-arm64` | `osx-arm64` | `.dylib` |

### 3. `publish.cs` — Release Orchestrator

**CLI Interface:**
```
dotnet scripts/publish.cs -- [OPTIONS]

OPTIONS:
  --version <semver>       Version to tag and pack (required)
  --feed <target>          NuGet target: pack-only, local, dev, staging, prod (default: pack-only)
  --skip-tests             Skip test execution
  --skip-native-build      Skip Rust compilation (artifacts must exist in artefacts-rust/)
  --create-release         Create GitHub release with artifacts using gh CLI
  --dry-run                Show plan without executing
```

**Pipeline Steps:**
```
1. Parse and validate arguments
2. [Unless --skip-native-build] Run build-native.cs for current platform
3. [Unless --skip-native-build] Run codegen if sources changed
4. Build .NET solution: dotnet build --configuration Release
5. [Unless --skip-tests] Run tests: dotnet tests-run.cs (or dotnet test)
6. Pack NuGet: dotnet pack --configuration Release -o packages/
7. Create git tag: git tag v{version} (using Process, not LibGit2Sharp)
8. [If --create-release] Create GitHub release:
   - gh release create v{version} --title "v{version}" --generate-notes
   - Attach all artifacts from artefacts-rust/**/* as release assets
9. [If feed != pack-only] Push NuGet: dotnet nuget push packages/*.nupkg
   - Use feed-specific env vars for URL and API key
10. Cleanup: Remove packages/ directory
11. Display summary
```

**Dependencies:** `CliWrap`, `Spectre.Console` (consistent with existing scripts).

## MSBuild Integration

### Refactored `Six.Automerge.FFI.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
        <RootNamespace>Six.Automerge.FFI</RootNamespace>
        <!-- Allow CI to skip Rust compilation when artifacts are pre-built -->
        <SkipRustBuild Condition="'$(SkipRustBuild)' == ''">false</SkipRustBuild>
    </PropertyGroup>

    <PropertyGroup>
        <ArtefactsRustRoot>$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)/../..', 'artefacts-rust'))</ArtefactsRustRoot>
        <NativeLibBaseName>libsix_automerge</NativeLibBaseName>
        <BuildNativeScript>$(MSBuildProjectDirectory)/../../scripts/build-native.cs</BuildNativeScript>
        <CopyNativeScript>$(MSBuildProjectDirectory)/../../scripts/copy-native-libs.cs</CopyNativeScript>
    </PropertyGroup>

    <!-- Build Rust native library (incremental, skippable in CI) -->
    <Target Name="BuildRustNative"
            BeforeTargets="Compile"
            Condition="'$(DesignTimeBuild)' != 'true'
                       and '$(SkipRustBuild)' != 'true'
                       and Exists('$(BuildNativeScript)')">
        <Exec Command="dotnet &quot;$(BuildNativeScript)&quot; -- --project &quot;$(MSBuildProjectDirectory)/native&quot; --run-codegen" />
    </Target>

    <!-- Copy native libs to output directory -->
    <Target Name="CopyNativeToOutput"
            AfterTargets="Build"
            Condition="'$(DesignTimeBuild)' != 'true' and Exists('$(CopyNativeScript)')">
        <PropertyGroup>
            <ResolvedOutDir>$([System.String]::Copy('$(TargetDir)').Replace('\','/'))</ResolvedOutDir>
        </PropertyGroup>
        <Exec Command="dotnet &quot;$(CopyNativeScript)&quot; -- --output-dir &quot;$(ResolvedOutDir)&quot; --artefacts-dir &quot;$(ArtefactsRustRoot)&quot;" />
    </Target>

    <!-- NuGet: Include native libs for all available platforms -->
    <!-- Windows x64 -->
    <ItemGroup Condition="Exists('$(ArtefactsRustRoot)/windows-x64/$(NativeLibBaseName)-windows-x64.dll')">
        <None Include="$(ArtefactsRustRoot)/windows-x64/$(NativeLibBaseName)-windows-x64.dll"
              Pack="true"
              PackagePath="runtimes/win-x64/native/$(NativeLibBaseName).dll" />
    </ItemGroup>
    <!-- Windows arm64 -->
    <ItemGroup Condition="Exists('$(ArtefactsRustRoot)/windows-arm64/$(NativeLibBaseName)-windows-arm64.dll')">
        <None Include="$(ArtefactsRustRoot)/windows-arm64/$(NativeLibBaseName)-windows-arm64.dll"
              Pack="true"
              PackagePath="runtimes/win-arm64/native/$(NativeLibBaseName).dll" />
    </ItemGroup>
    <!-- Linux x64 -->
    <ItemGroup Condition="Exists('$(ArtefactsRustRoot)/linux-x64/$(NativeLibBaseName)-linux-x64.so')">
        <None Include="$(ArtefactsRustRoot)/linux-x64/$(NativeLibBaseName)-linux-x64.so"
              Pack="true"
              PackagePath="runtimes/linux-x64/native/$(NativeLibBaseName).so" />
    </ItemGroup>
    <!-- Linux arm64 -->
    <ItemGroup Condition="Exists('$(ArtefactsRustRoot)/linux-arm64/$(NativeLibBaseName)-linux-arm64.so')">
        <None Include="$(ArtefactsRustRoot)/linux-arm64/$(NativeLibBaseName)-linux-arm64.so"
              Pack="true"
              PackagePath="runtimes/linux-arm64/native/$(NativeLibBaseName).so" />
    </ItemGroup>
    <!-- macOS x64 -->
    <ItemGroup Condition="Exists('$(ArtefactsRustRoot)/macos-x64/$(NativeLibBaseName)-macos-x64.dylib')">
        <None Include="$(ArtefactsRustRoot)/macos-x64/$(NativeLibBaseName)-macos-x64.dylib"
              Pack="true"
              PackagePath="runtimes/osx-x64/native/$(NativeLibBaseName).dylib" />
    </ItemGroup>
    <!-- macOS arm64 -->
    <ItemGroup Condition="Exists('$(ArtefactsRustRoot)/macos-arm64/$(NativeLibBaseName)-macos-arm64.dylib')">
        <None Include="$(ArtefactsRustRoot)/macos-arm64/$(NativeLibBaseName)-macos-arm64.dylib"
              Pack="true"
              PackagePath="runtimes/osx-arm64/native/$(NativeLibBaseName).dylib" />
    </ItemGroup>
</Project>
```

### Consuming Projects (14 total)

**Before (each project):**
```xml
<Target Name="CopyNativeDependencies" AfterTargets="Build">
    <ItemGroup>
        <_NativeArtifacts Include="..\..\libs\Six.Automerge.FFI\bin\$(Configuration)\**\libsix_automerge.dll"/>
        <_NativeArtifacts Include="..\..\libs\Six.Automerge.FFI\bin\$(Configuration)\**\libsix_automerge.so"/>
        <_NativeArtifacts Include="..\..\libs\Six.Automerge.FFI\bin\$(Configuration)\**\libsix_automerge.dylib"/>
    </ItemGroup>
    <Copy SourceFiles="@(_NativeArtifacts)" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" Condition="Exists('%(Identity)')" />
</Target>
```

**After:** The entire `CopyNativeDependencies` target is removed. Native libraries are already in the FFI project's output directory (placed there by `CopyNativeToOutput` target), and MSBuild's `ReferenceCopyLocalPaths` propagates them through project references.

## NuGet Package Structure

### `Six.Automerge.FFI.1.0.0-alpha.1.nupkg`

```
/
├── lib/net10.0/
│   ├── Six.Automerge.FFI.dll
│   └── Six.Automerge.FFI.xml
├── runtimes/
│   ├── win-x64/native/libsix_automerge.dll
│   ├── win-arm64/native/libsix_automerge.dll
│   ├── linux-x64/native/libsix_automerge.so
│   ├── linux-arm64/native/libsix_automerge.so
│   ├── osx-x64/native/libsix_automerge.dylib
│   └── osx-arm64/native/libsix_automerge.dylib
├── README.md
├── project_icon.png
└── Six.Automerge.FFI.nuspec
```

Consumers reference `Six.Automerge.FFI` and .NET automatically extracts the correct native library for their platform from the `runtimes/` directory.

## GitHub Actions Workflow Design

### `build-test-publish.yml`

```yaml
name: Build, Test & Publish

on:
  workflow_dispatch:
    inputs:
      version:
        description: 'Version (e.g., 1.0.0-alpha.2)'
        required: true
        type: string
      feed:
        description: 'NuGet feed target'
        required: true
        type: choice
        default: 'pack-only'
        options: [pack-only, local, dev, staging, prod]
      create_release:
        description: 'Create GitHub Release'
        required: true
        type: boolean
        default: true

jobs:
  build-native:
    strategy:
      fail-fast: true
      matrix:
        include:
          - { target_os: windows, target_arch: x64,    runner: windows-latest }
          - { target_os: windows, target_arch: arm64,  runner: windows-latest }
          - { target_os: linux,   target_arch: x64,    runner: ubuntu-latest  }
          - { target_os: linux,   target_arch: arm64,  runner: ubuntu-latest  }
          - { target_os: macos,   target_arch: x64,    runner: macos-13       }
          - { target_os: macos,   target_arch: arm64,  runner: macos-14       }
    runs-on: ${{ matrix.runner }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - name: Install cross-compilation tools  # conditional per target
      - name: Build native
        run: dotnet scripts/build-native.cs -- --target-os ${{ matrix.target_os }} --target-arch ${{ matrix.target_arch }} --force --verbose
      - uses: actions/upload-artifact@v4
        with:
          name: native-${{ matrix.target_os }}-${{ matrix.target_arch }}
          path: artefacts-rust/${{ matrix.target_os }}-${{ matrix.target_arch }}/

  verify-codegen:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - name: Build native + codegen
        run: dotnet scripts/build-native.cs -- --run-codegen --force --verbose
      - name: Verify Interop.g.cs matches committed version
        run: git diff --exit-code libs/Six.Automerge.FFI/generated/Interop.g.cs

  test:
    needs: build-native
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - uses: actions/download-artifact@v4
        with: { name: native-linux-x64, path: artefacts-rust/linux-x64/ }
      - name: Build and test
        run: |
          dotnet build --configuration Release -p:SkipRustBuild=true
          dotnet tests-run.cs

  release:
    needs: [build-native, verify-codegen, test]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - name: Download all native artifacts
        # Download all 6 platform artifacts into artefacts-rust/
      - name: Publish
        run: >
          dotnet scripts/publish.cs --
          --version ${{ inputs.version }}
          --feed ${{ inputs.feed }}
          --skip-native-build
          --skip-tests
          ${{ inputs.create_release && '--create-release' || '' }}
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          NUGET_FEED_DEV_URL: ${{ secrets.NUGET_FEED_DEV_URL }}
          # ... other feed env vars as needed
```

### Cross-Compilation Setup

**Linux arm64 on Ubuntu x64:**
- Install: `gcc-aarch64-linux-gnu`
- Rust target: `aarch64-unknown-linux-gnu`
- Linker configured in `.cargo/config.toml`

**Windows arm64 on Windows x64:**
- Rust target: `aarch64-pc-windows-msvc`
- MSVC cross-compilation toolchain (bundled with Visual Studio Build Tools on `windows-latest`)

**macOS:**
- x64 builds on `macos-13` (Intel runner) — native compilation, no cross-compile needed
- arm64 builds on `macos-14` (ARM runner) — native compilation, no cross-compile needed

## Rust Toolchain Configuration

### `libs/Six.Automerge.FFI/native/rust-toolchain.toml`

```toml
[toolchain]
channel = "stable"
targets = [
    "x86_64-pc-windows-msvc",
    "aarch64-pc-windows-msvc",
    "x86_64-unknown-linux-gnu",
    "aarch64-unknown-linux-gnu",
    "x86_64-apple-darwin",
    "aarch64-apple-darwin",
]
```

### `libs/Six.Automerge.FFI/native/.cargo/config.toml`

```toml
[target.aarch64-unknown-linux-gnu]
linker = "aarch64-linux-gnu-gcc"
```

## Data Flow Diagrams

### Local Development Flow

```
Developer runs: dotnet build
         │
         ▼
  Six.Automerge.FFI.csproj
  BuildRustNative target
         │
         ▼
  build-native.cs (auto-detect platform)
  ┌─ checksums changed? ─┐
  │ NO: skip             YES: cargo build --release
  │                            │
  │                            ▼
  │                   artefacts-rust/{os}-{arch}/
  │                   libsix_automerge-{os}-{arch}.{ext}
  └──────────┬─────────────────┘
             ▼
  CopyNativeToOutput target
  copy-native-libs.cs
         │
         ▼
  bin/Release/net10.0/
  ├── libsix_automerge.{ext}          (P/Invoke finds this)
  └── runtimes/{rid}/native/
      └── libsix_automerge.{ext}      (RID probing backup)
         │
         ▼
  Consuming projects get native lib via
  ReferenceCopyLocalPaths propagation
```

### CI Release Flow

```
Manual dispatch: version=1.0.0-alpha.2, feed=prod, create_release=true
         │
         ▼
  ┌──────────────────────────────────────────────┐
  │ build-native (6 parallel matrix jobs)         │
  │ windows-x64 │ win-arm64 │ linux-x64 │ ...    │
  │ Each uploads to: native-{os}-{arch} artifact  │
  └──────────────────────┬───────────────────────┘
                         │
  ┌──────────────┐       │       ┌───────────┐
  │verify-codegen│       │       │   test     │
  │ (linux-x64)  │       │       │(linux-x64) │
  └──────┬───────┘       │       └─────┬─────┘
         │               │             │
         └───────────────┼─────────────┘
                         ▼
              ┌──────────────────┐
              │   release job    │
              │ Downloads ALL 6  │
              │ artifacts into   │
              │ artefacts-rust/  │
              │                  │
              │ publish.cs:      │
              │ 1. dotnet pack   │
              │ 2. git tag       │
              │ 3. gh release    │
              │ 4. nuget push    │
              └──────────────────┘
```
