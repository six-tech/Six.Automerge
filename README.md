## 🚀 What is Six.Automerge?

**Six.Automerge** are production-ready C# bindings for a Rust-based library [Automerge](https://github.
com/automerge/automerge) that make it easy to build collaborative applications using CRDT structures.


## AI Agent Reference for creating FFI Code and Bindings

- Use (Interoptopus)[https://github.com/ralfbiedert/interoptopus] to generate C# bindings for the Rust library.
- Use the `.externs/interoptopus/examples/real_project_layout/` directory as an example usage how to prepare and create Rust bindings for C#.


## 🏗️ Project Structure

```
Six.Automerge/                                  # Root directory of the solution
├── apps/                                       # Application projects
│   └── DemoApp.Basic/                          # Demo application demonstrating the Automerge features in C#
├── libs/                                       # Library projects
│   └── Six.Automerge/                          # C# bindings for the Automerge Rust library
│   └── Six.Automerge.FFI/                      # Rust FFI code for the Automerge and low level C# library
│       ├── src/                                # Low-level C# library with direct bindings to Rust FFI code
│       └── native/                             # Rust FFI code
├── tests/                                      # Test projects
│   └── Six.Automerge.FFI.Test.Unit/            # Unit tests for the C# low level library
├── scripts/                                    # C# build scripts (requires .NET 10)
│   ├── pack.cs                                 # NuGet packaging script
│   ├── tests-run.cs                            # Test execution script
│   ├── tests-coverage.cs                       # Coverage collection script
│   └── tests-report.cs                         # Coverage reporting script
├── docs/                                       # In-Repository Astro.js/Starlight documentation site
├── .references/                                # Directories with Rust source files of Automerge; for AI agent reference to be able to see the native APIs when writing FFI code and bindings.
├── .github/                                    # GitHub workflows and templates
└── Configuration files                         # Global project settings in the solution root (global.json, nuget.config, .editorconfig, etc.)
```


## 🛠️ Prerequisites

- **.NET 10 SDK**: Required for C# file-based apps (build scripts)
- **pnpm/npm**: For documentation site build


### Key Components
- **Application Projects**: Located in `apps/` directory
- **Library Projects**: Located in `libs/` directory
- **Test Projects**: Located in `tests/` directory
- **Build Scripts**: **C# file-based apps** in `scripts/` directory (requires .NET 10)
- **Documentation**: **Astro.js** Starlight site in `docs/` directory
- **CI/CD**: GitHub Actions workflows in `.github/workflows/`

## ✨ Key Features

### 🎯 **Project Structure & Configuration**
- **Global .NET Version Management**: Configured with .NET 10 (.NET 10.0.100-rc.1 currently) in `global.json`
- **Language Features**: C# 13, nullable reference types, implicit usings enabled
- **Directory Organization**: Clear separation of `apps/`, `libs/`, `tests/`, and `benchmarks/` projects

### 🔧 **Code Quality & Standards**
- **StyleCop Integration**: Automated code style enforcement with `stylecop.json`
- **EditorConfig**: Consistent coding standards across all IDEs
- **Code Analysis**: Built-in rule sets and analysis configuration
- **Treat Warnings as Errors**: Ensures high code quality standards

### 📦 **NuGet Package Management**
- **Environment-Based Configuration**: Support for local, development, staging, and public NuGet feeds
- **Automated Publishing**: C# scripts for packaging and publishing (requires .NET 10)
- **Package Metadata**: Centralized package configuration in `Directory.Build.props`

### 🧪 **Testing & Quality Assurance**
- **C# Scripts**: File-based apps for testing workflows (requires .NET 10)
- **Code Coverage**: Automated coverage collection and reporting

### 📚 **Documentation & Publishing**
- **Astro.js Starlight**: Modern documentation site with **Six theme**
- **GitHub Pages**: Automatic deployment **on push to main branch**
- **Beautiful & Modern Theme**: With Six theme; clean, modern, and responsive design
- **Search & Navigation**: Full-text search and organized navigation

### 🧩 **FFI Regeneration & Build Workflow**
- **Regenerate bindings:** `cargo run --offline --manifest-path libs/Six.Automerge.FFI/codegen/Cargo.toml`
- **Build native library:** `cargo build --release --offline --manifest-path libs/Six.Automerge.FFI/native/Cargo.toml`
- **Managed build:** `dotnet build` (invokes the steps above automatically inside `Six.Automerge.FFI.csproj`)
- **Offline note:** If NuGet feeds are unavailable, add `NU1900` to `WarningsNotAsErrors` (already configured) or temporarily pass `--ignore-failed-sources` during restore.


----

**Built with ❤️ by [Six Technology](https://six.technology)**
