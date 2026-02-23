# Six.Automerge.FFI Build Configuration

## Platform-Specific Build Targets

To avoid cross-platform Rust artifact conflicts when building on different operating systems or in WSL, this project uses platform-specific target directories for the Rust codegen.

### Automatic Platform Detection

The build system automatically detects your platform and uses the appropriate target directory:

| Platform | Detection | Target Directory |
|----------|-----------|------------------|
| **Windows** | `$(OS) == 'Windows_NT'` | `codegen/target-windows` |
| **Linux** | `uname -s` == `Linux` | `codegen/target-linux` |
| **macOS** | `uname -s` == `Darwin` | `codegen/target-macos` |
| **Other Unix** | Fallback | `codegen/target-unix` |

### How It Works

1. **DetectPlatform Target**: Runs `uname -s` on Unix systems to distinguish Linux from macOS
2. **BuildNative Target**: Compiles the Rust native library
3. **GenerateInterop Target**: Runs the Rust codegen using the platform-specific target directory

### Building

Simply run:
```bash
dotnet build Six.Automerge.FFI.csproj
```

The build will automatically:
- Detect your platform
- Use the correct target directory
- Build the native Rust library
- Generate C# interop code via Interoptopus

### WASM Support

For WebAssembly builds, you would need a separate build configuration:

1. Install wasm-pack: `cargo install wasm-pack`
2. Build with: `wasm-pack build --target web` (or other targets)
3. This would require separate MSBuild targets or a dedicated build script

WASM builds are not included in the standard dotnet build process as they target a completely different runtime environment.

### Troubleshooting

**Issue**: Build fails with "couldn't find crate with expected target triple"

**Solution**: This occurs when switching between Windows and WSL builds. The platform-specific directories should prevent this, but if you encounter it:

```bash
# Manually clean codegen artifacts
cd libs/Six.Automerge.FFI/codegen
cargo clean --manifest-path=Cargo.toml
```

**Issue**: Platform not detected correctly

**Solution**: The build will output: `Detected platform: <name>, using target directory: <path>`.
Check this message to verify detection. If incorrect, you can manually set `CodegenTargetDir`:

```bash
dotnet build /p:CodegenTargetDir="libs/Six.Automerge.FFI/codegen/target-custom"
```

### Build Artifacts

All platform-specific directories are gitignored:
- `target-windows/`
- `target-linux/`
- `target-macos/`
- `target-unix/`
- `target-wasm/`

This allows developers to work on multiple platforms without conflicts in version control.
