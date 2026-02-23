# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

`Six.Automerge` is a production-grade C# bindings library for [Automerge](https://automerge.org/), a Rust-based CRDT (Conflict-free Replicated Data Type) library. It provides low-level FFI bindings, a high-level C# API, and a repository pattern for document synchronization across peers.

## Automerge Documentation
LLM ready documentation for Automerge library is available at [automerge LLM docs](https://automerge.org/llms-full.txt).

## Build & Development

**Prerequisites:** .NET 10 SDK, Rust toolchain (for native FFI compilation)

```bash
dotnet build                              # Build everything (triggers Rust FFI compilation automatically)
dotnet build --no-restore                 # Skip restore
dotnet test tests/Six.Automerge.FFI.Tests.Unit  # Run a single test project
dotnet tests-run.cs                       # Run all test projects via script
dotnet tests-coverage.cs                  # Run tests with code coverage
dotnet tests-report.cs                    # Generate coverage report
dotnet pack.cs --local                    # Pack NuGet to local feed
```

**Run a single test by name:**
```bash
dotnet test tests/Six.Automerge.FFI.Tests.Unit --filter "FullyQualifiedName~CreateCommitAndHydrate"
```

**No .sln file** — use `dotnet build` from the repo root or target individual `.csproj` files directly.

**Build scripts** live in `scripts/` and use .NET 10's file-based C# script format (`#!/usr/bin/env dotnet`).

## Architecture

### Layer Structure

```
┌─────────────────────────────────────────────────────────┐
│  apps/                                                   │
│  DemoApp.Basic, DemoApp.Avalonia, Server.Api,           │
│  Server.Silo (Orleans), Infra.AppHost (.NET Aspire)     │
├─────────────────────────────────────────────────────────┤
│  Six.Automerge.Repo          (document lifecycle, sync)  │
│  + Network impls: SignalR.Client/Server, OrleansSync     │
│  + Storage impls: FileSystem                             │
├─────────────────────────────────────────────────────────┤
│  Six.Automerge.Repo.Abstractions                         │
│  IStorageAdapter, INetworkAdapter, DocumentId, StorageKey │
├─────────────────────────────────────────────────────────┤
│  Six.Automerge                (high-level C# API)        │
│  AutomergeDocument, AutomergePatch, AutomergeSyncState   │
├─────────────────────────────────────────────────────────┤
│  Six.Automerge.FFI            (P/Invoke via Interoptopus)│
│  Generated: Interop.g.cs ← Rust codegen                 │
├─────────────────────────────────────────────────────────┤
│  native Rust (libs/Six.Automerge.FFI/native/)            │
│  six_automerge crate → automerge 0.7.0                   │
└─────────────────────────────────────────────────────────┘
```

### Key Subsystems in Six.Automerge.Repo

- **Repo** — main entry point managing document lifecycle and peer connections
- **StorageSubsystem** / **IStorageAdapter** — pluggable persistence (InMemory, FileSystem)
- **NetworkSubsystem** / **INetworkAdapter** — pluggable transport (InMemory, SignalR, Orleans)
- **CollectionSynchronizer** / **DocSynchronizer** — orchestrates Automerge sync protocol across peers
- **DocHandle** / **IDocHandle** — document reference with change events

### Rust FFI Build Pipeline

Building `Six.Automerge.FFI` triggers an MSBuild target that:
1. Runs `scripts/build-six-automerge-ffi.cs` (incremental, checksum-based)
2. Compiles Rust crate via `cargo build --release` into platform-specific `target-{windows,linux,macos}/`
3. Runs Interoptopus codegen (`codegen/`) to generate `generated/Interop.g.cs`
4. Copies native library (`libsix_automerge.dll` / `libsix_automerge.so` / `libsix_automerge.dylib`) to output

The FFI layer uses `AllowUnsafeBlocks`. The Rust source is in `libs/Six.Automerge.FFI/native/src/` (doc.rs, sync.rs, types.rs, error.rs, util.rs).

## Code Style & Conventions

- **C# 13**, nullable reference types enabled, `TreatWarningsAsErrors` is on
- **StyleCop** enforced (MIT copyright header from `stylecop.json`, company "Six")
- **EditorConfig**: 2-space indentation, file-scoped namespaces, Allman braces, no `var` for non-apparent types
- Private readonly fields use `_camelCase` prefix
- Accessibility modifiers omitted for private members (`dotnet_style_require_accessibility_modifiers = never`)
- No space after keywords in control flow (`if(`, `for(`, `while(`)
- `[PublicAPI]` attribute from JetBrains.Annotations used to mark public surface

## Testing

- **xUnit** with `[Fact]` / `[Theory]` attributes
- Tests organized by `[Trait("Category", "...")]`
- Test projects follow naming: `*.Tests.Unit` and `*.Tests.Integration`
- Integration tests (SignalR, FileSystem, AppHost) may require Docker

## .NET Aspire Infrastructure

The `apps/Infra.AppHost/` project orchestrates the full distributed stack via .NET Aspire (SDK 9.5.1). It requires Docker for the Azure Storage emulator.

**Start the full stack:**
```bash
dotnet run --project apps/Infra.AppHost        # Launches Aspire dashboard + all services
```

This opens the Aspire dashboard (default `http://localhost:15254`) which shows all resources, logs, traces, and metrics.

### What Aspire Orchestrates

The AppHost (`apps/Infra.AppHost/src/AppHost.cs`) wires up:

1. **Azure Storage Emulator (Azurite)** — runs as a container via `RunAsEmulator()`, provides table storage for Orleans clustering and grain state
2. **Server.Silo** — Orleans grain host, joins the cluster as a silo (1 replica by default)
3. **Server.Api** — REST API, connects as an Orleans client, waits for silo startup. Exposes external HTTP endpoints with OpenAPI in dev mode
4. **DemoApp.Avalonia** — desktop client, configured with `WithExplicitStart()` (must be started manually from the dashboard) and 3 replicas for testing multi-peer Automerge sync. Excluded from distributed manifest

### Shared Infrastructure Projects

- **Infra.Consts** (`apps/Infra.Consts/Consts.cs`) — shared resource name constants used by both AppHost and services (e.g., `AzureStorage`, `ClusteringServiceClient`, `GrainStateServiceClient`). Referenced with `IsAspireProjectResource="false"` so Aspire doesn't try to launch it
- **Infra.ServiceDefaults** (`apps/Infra.ServiceDefaults/Extensions.cs`) — standard Aspire service defaults applied to all services via `builder.AddServiceDefaults()`. Configures:
  - OpenTelemetry (tracing + metrics for ASP.NET Core, HTTP, Runtime, and Orleans)
  - Health checks (`/health` for readiness, `/alive` for liveness)
  - Service discovery and HTTP resilience

### Orleans Integration

Orleans is configured through Aspire with Azure Table Storage for both clustering and grain persistence:

```
AppHost ─→ AddOrleans("orleans-service")
            ├── WithClustering(azureClusteringTable)
            └── WithGrainStorage("RayGrainStorage", azureGrainStorageTable)

Server.Silo ─→ builder.UseOrleans()           // joins as silo
Server.Api  ─→ builder.UseOrleansClient()     // connects as client
```

The `DocumentGrain` in `Server.Silo/src/Grains/` uses `IPersistentState<DocumentState>` for durable state. The grain interface `IDocumentGrain : IGrainWithGuidKey` is currently a scaffold — grain methods are commented out in the interface, ready for Automerge document operations to be wired in.

### SignalR Network Layer

For peer-to-peer document sync over the network, the SignalR stack provides:

- **Six.Automerge.Repo.Network.SignalR.Abstractions** — hub interfaces (`IDocumentsHub`, `IDocumentsHubClient`)
- **Six.Automerge.Repo.Network.SignalR.Server** — `DocumentsHub` implementation, registered via `builder.Services.AddSignalRDocumentsHub()` and mapped to `/hubs/documents`
- **Six.Automerge.Repo.Network.SignalR.Client** — `SignalRNetworkAdapter` (implements `INetworkAdapter`), `SignalRConnectionService` for managing hub connections

The SignalR test server (`tests/Six.SignalR.TestServer/`) is a minimal ASP.NET Core app that hosts the DocumentsHub for integration testing. It is orchestrated by the test AppHost (`tests/Six.Tests.AppHost/`) via Aspire on port 8080.

### Test AppHost

`tests/Six.Tests.AppHost/` is a separate Aspire host for integration tests. It orchestrates:
- The SignalR test server with a fixed HTTP endpoint (port 8080) and health check at `/health`

Run integration tests that depend on it:
```bash
dotnet test tests/Six.Automerge.Repo.Network.SignalR.Tests.Integration
```

## End-to-End Usage of the Automerge API

### Low-Level: AutomergeDocument (Six.Automerge)

```csharp
using var doc = AutomergeDocument.Create();
var root = doc.RootId;

// Write data (map operations)
doc.PutString(root, "title", "My Document");
doc.PutInt64(root, "priority", 1);
doc.PutBoolean(root, "done", false);
doc.PutCounter(root, "views", 0);
doc.PutTimestamp(root, "created", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// Nested containers
var tasks = doc.PutObject(root, "tasks", AutomergeContainerType.List);
var task = doc.ListInsertObject(tasks, 0, AutomergeContainerType.Map);
doc.PutString(task, "name", "First task");

// Text CRDT
var desc = doc.PutObject(task, "description", AutomergeContainerType.Text);
doc.SpliceText(desc, 0, 0, "Hello world");

// Commit changes
doc.Commit("Added initial data", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

// Hydrate to JSON
string json = doc.HydrateJson();

// Save/Load (full persistence)
byte[] saved = doc.Save();
using var restored = AutomergeDocument.Load(saved);

// Incremental save/load
byte[] incremental = doc.SaveIncremental();
int applied = otherDoc.LoadIncremental(incremental);
```

### Sync Protocol (peer-to-peer)

```csharp
using var syncAB = docA.CreateSyncState();
using var syncBA = docB.CreateSyncState();

// Exchange messages until converged
for (int i = 0; i < 10; i++)
{
    var msgA = docA.GenerateSyncMessage(syncAB);
    var msgB = docB.GenerateSyncMessage(syncBA);
    if (msgA == null && msgB == null) break;
    if (msgA != null) docB.ReceiveSyncMessage(syncBA, msgA);
    if (msgB != null) docA.ReceiveSyncMessage(syncAB, msgB);
}
```

### Repository Layer (Six.Automerge.Repo)

The `Repo` class manages document lifecycle with pluggable storage and network adapters. See `DemoApp.Basic/Program.cs` for a full 3-peer collaborative sync demo.

## NuGet Packaging

`dotnet pack.cs` discovers all `.csproj` in `libs/` and publishes to configured feeds. Supports `--local`, `--dev`, `--staging`, `--prod` flags. Requires environment variables for feed URLs and credentials (see `nuget.config`).
