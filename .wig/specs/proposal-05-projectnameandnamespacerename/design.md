# Design: Project Name and Namespace Rename (Ray -> Six)

## Overview

This document describes the technical design for renaming all `Ray.*` references to `Six.*` across the entire codebase: C# namespaces, Rust crate names, native artifact names, build scripts, and documentation.

## Namespace Mapping

Direct prefix substitution — `Ray.` becomes `Six.`, the rest of each namespace path is unchanged.

### C# Namespace Map

| Old Namespace | New Namespace |
|---|---|
| `Six.Automerge` | `Six.Automerge` |
| `Six.Automerge.FFI` | `Six.Automerge.FFI` |
| `Six.Automerge.FFI.Native` | `Six.Automerge.FFI.Native` |
| `Six.Automerge.Repo` | `Six.Automerge.Repo` |
| `Six.Automerge.Repo.Abstractions` | `Six.Automerge.Repo.Abstractions` |
| `Six.Automerge.Repo.Abstractions.Network` | `Six.Automerge.Repo.Abstractions.Network` |
| `Six.Automerge.Repo.Abstractions.Network.Messages` | `Six.Automerge.Repo.Abstractions.Network.Messages` |
| `Six.Automerge.Repo.Abstractions.Storage` | `Six.Automerge.Repo.Abstractions.Storage` |
| `Six.Automerge.Repo.Abstractions.Synchronizer` | `Six.Automerge.Repo.Abstractions.Synchronizer` |
| `Six.Automerge.Repo.Network` | `Six.Automerge.Repo.Network` |
| `Six.Automerge.Repo.Network.SignalR` | `Six.Automerge.Repo.Network.SignalR` |
| `Six.Automerge.Repo.Network.SignalR.Abstractions` | `Six.Automerge.Repo.Network.SignalR.Abstractions` |
| `Six.Automerge.Repo.Network.SignalR.Server` | `Six.Automerge.Repo.Network.SignalR.Server` |
| `Six.Automerge.Repo.Storage` | `Six.Automerge.Repo.Storage` |
| `Six.Automerge.Repo.Storage.FileSystem` | `Six.Automerge.Repo.Storage.FileSystem` |
| `Six.Automerge.Repo.Synchronizer` | `Six.Automerge.Repo.Synchronizer` |
| `Six.Automerge.Repo.Testing` | `Six.Automerge.Repo.Testing` |
| `Six.Automerge.Repo.Helpers` | `Six.Automerge.Repo.Helpers` |
| `Six.Automerge.Repository` (docs only) | `Six.Automerge.Repository` |
| `Six.Automerge.Repository.*` (docs only) | `Six.Automerge.Repository.*` |
| `Ray.SignalR.TestServer` | `Six.SignalR.TestServer` |

### Rust Crate Map

| Old | New |
|---|---|
| `ray_automerge` (crate) | `six_automerge` |
| `ray_automerge_codegen` (crate) | `six_automerge_codegen` |

## Native Artifact Naming Strategy

### Current State

| Platform | Artifact Name | DllImport Name |
|---|---|---|
| Windows | `ray_automerge.dll` | `ray_automerge` |
| Linux | `libray_automerge.so` | `ray_automerge` |
| macOS | `libray_automerge.dylib` | `ray_automerge` |

### Target State (Unified `lib` Prefix)

| Platform | Artifact Name | DllImport Name |
|---|---|---|
| Windows | `libsix_automerge.dll` | `libsix_automerge` |
| Linux | `libsix_automerge.so` | `libsix_automerge` |
| macOS | `libsix_automerge.dylib` | `libsix_automerge` |

### How This Works

1. **Rust output**: Crate `[lib] name = "six_automerge"` produces:
   - Windows: `six_automerge.dll`
   - Linux: `libsix_automerge.so`
   - macOS: `libsix_automerge.dylib`

2. **Build script post-processing**: On Windows, rename `six_automerge.dll` to `libsix_automerge.dll` during the copy step. On Linux/macOS, the output already has the `lib` prefix naturally.

3. **DllImport resolution**: Using `DllImport("libsix_automerge")`:
   - Windows: .NET looks for `libsix_automerge.dll` (found after rename)
   - Linux: .NET tries `liblibsix_automerge.so` (miss), then `libsix_automerge.so` (found)
   - macOS: .NET tries `liblibsix_automerge.dylib` (miss), then `libsix_automerge.dylib` (found)

4. **Codegen config**: `Interop::builder().dll_name("libsix_automerge")` ensures the generated `Interop.g.cs` uses the unified name.

## Build Script Changes

### File Rename
`scripts/build-ray-automerge-ffi.cs` -> `scripts/build-six-automerge-ffi.cs`

### Content Changes

```csharp
// Before
const string LibraryName = "Six.Automerge.FFI";
const string NativeCrateName = "ray_automerge";
// ...
"ray_automerge.dll",
"libray_automerge.so",
"libray_automerge.dylib"

// After
const string LibraryName = "Six.Automerge.FFI";
const string NativeCrateName = "six_automerge";
// ...
```

### Artifact Name Generation

The `GetNativeLibraryName` function changes to produce `lib`-prefixed names on all platforms:

```csharp
// Before
static string GetNativeLibraryName(string targetSuffix)
{
    return targetSuffix switch
    {
        "windows" => $"{NativeCrateName}.dll",
        "macos" => $"lib{NativeCrateName}.dylib",
        _ => $"lib{NativeCrateName}.so"
    };
}

// After
static string GetNativeLibraryName(string targetSuffix)
{
    return targetSuffix switch
    {
        "windows" => $"lib{NativeCrateName}.dll",
        "macos" => $"lib{NativeCrateName}.dylib",
        _ => $"lib{NativeCrateName}.so"
    };
}
```

The `CopyNativeOutputs` function must also handle the Windows rename (cargo outputs `six_automerge.dll`, we copy as `libsix_automerge.dll`):

```csharp
static void CopyNativeOutputs(string nativeTargetDir, string buildProfile, string outputDirectory)
{
    // Map from Rust build output name -> desired output name
    var targets = new (string Source, string Destination)[]
    {
        ("six_automerge.dll", "libsix_automerge.dll"),
        ("libsix_automerge.so", "libsix_automerge.so"),
        ("libsix_automerge.dylib", "libsix_automerge.dylib"),
    };
    // ...copy with rename...
}
```

## FFI .csproj Changes

The `Six.Automerge.FFI.csproj` MSBuild targets must reference:
- Script path: `../../scripts/build-six-automerge-ffi.cs`
- Artifact names: `libsix_automerge.dll`, `libsix_automerge.so`, `libsix_automerge.dylib`

All `<NativeOutputs>` and `<None>` items updated to use the new names.

## Rust Codegen Changes

`codegen/src/main.rs`:
```rust
// Before
let inventory = ray_automerge::ffi_inventory();
let namespace = NamespaceMappings::new("Six.Automerge.FFI.Native");
Interop::builder()
    .inventory(inventory)
    .dll_name("ray_automerge")

// After
let inventory = six_automerge::ffi_inventory();
let namespace = NamespaceMappings::new("Six.Automerge.FFI.Native");
Interop::builder()
    .inventory(inventory)
    .dll_name("libsix_automerge")
```

## Scripting Strategy for Mass C# Rename

Use `sed` or equivalent shell command across all `.cs` files (excluding `.references/`, `node_modules/`, `obj/`, `bin/`):

**Pattern 1 — Namespaces and usings:**
```
Six.Automerge -> Six.Automerge
Ray.SignalR -> Six.SignalR
```

**Pattern 2 — Remaining string references:**
```
Six.Automerge -> Six.Automerge
```

**Exclusions:**
- `.references/` directory
- `node_modules/` directory
- `obj/` and `bin/` directories
- Cargo `target*/` directories

The same approach applies to `.mdx` and `.md` documentation files.

## Documentation Site Changes

`docs/astro.config.mjs`:
```javascript
// Before
base: '/Six.Automerge/',
site: 'https://ray-platform.github.io',
title: 'Six.Automerge',

// After
base: '/Six.Automerge/',
site: 'https://six-tech.github.io',
title: 'Six.Automerge',
```

## Cargo Target Cleanup

All platform-specific target directories contain cached fingerprints referencing `ray_automerge`. These must be fully cleaned:

```
libs/Six.Automerge.FFI/native/target-windows/
libs/Six.Automerge.FFI/native/target-linux/
libs/Six.Automerge.FFI/native/target/
libs/Six.Automerge.FFI/codegen/target-windows/
libs/Six.Automerge.FFI/codegen/target-linux/
libs/Six.Automerge.FFI/codegen/target/
```

After cleanup, rebuild triggers fresh compilation with the new crate names.

## Risk Considerations

1. **Generated Interop.g.cs**: This file is auto-generated by Rust codegen. After updating the codegen config and rebuilding, it will contain `Six.Automerge.FFI.Native` namespace and `libsix_automerge` DllImport name. The C# namespace rename must happen before or simultaneously with the regeneration to avoid build breaks.

2. **Windows lib prefix**: The `lib` prefix on Windows DLLs is non-standard. The `.NET NativeLibrary` resolver handles this correctly when the DllImport name includes the `lib` prefix explicitly. This has been validated against .NET runtime behavior.

3. **Docs data-store.json**: The `docs/.astro/data-store.json` is auto-generated and will regenerate on next docs build. No manual edit needed.
