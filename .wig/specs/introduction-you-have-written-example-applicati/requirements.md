# Requirements: Multi-Client Sync Demo Apps

## Overview

All four demo Avalonia applications (CollaborativeEditor, CollaborativeTextEditor, FigmaClone, TaskBoard) must be upgraded from simulated in-process sync to real multi-client synchronization over SignalR, using the Repo pattern with FileSystem storage, orchestrated via .NET Aspire.

## Functional Requirements

### FR-1: Server Infrastructure

- **FR-1.1**: Server.Api must host a SignalR DocumentsHub at `/hubs/documents` using `AddSignalRDocumentsHub()` (MessagePack + LZ4 compression).
- **FR-1.2**: Server.Api must expose a health check endpoint for Aspire readiness.
- **FR-1.3**: CORS policy on Server.Api must allow SignalR WebSocket connections with credentials (change from `AllowAnyOrigin` to specific origins with `AllowCredentials`).

### FR-2: Aspire Orchestration

- **FR-2.1**: AppHost must register all four demo apps with configurable replica counts (default 2 per app).
- **FR-2.2**: All demo apps must have `WithExplicitStart()` so they are started manually from the Aspire dashboard.
- **FR-2.3**: All demo apps must reference Server.Api and wait for it to be ready before connecting.
- **FR-2.4**: Demo apps must receive the Server.Api URL via Aspire service discovery (environment variable injection).
- **FR-2.5**: Demo apps must be excluded from the distributed manifest (they are desktop apps, not services).

### FR-3: Shared App Infrastructure (applies to all four apps)

- **FR-3.1**: Each app must use the `Repo` class with `SignalRNetworkAdapter` and `FileSystemStorageAdapter`.
- **FR-3.2**: Each app instance must generate a unique `PeerId` on startup.
- **FR-3.3**: Each app must display a **Connection Status Panel** showing:
  - Own PeerId (truncated)
  - SignalR connection state (Connecting / Connected / Disconnected / Reconnecting)
  - Number of connected peers
  - Current document ID (copiable to clipboard)
  - Server URL being connected to
- **FR-3.4**: Each app must support two modes:
  - **Create mode**: Create a new document and share the document ID.
  - **Join mode**: Enter an existing document ID (paste from clipboard) to join an existing session.
- **FR-3.5**: Documents must persist across app restarts via FileSystem storage.
- **FR-3.6**: When a remote peer makes changes, the local UI must update within 1 second.
- **FR-3.7**: Remove all existing `SyncService` timer-based fork sync mechanisms.
- **FR-3.8**: Remove all "Simulate Peer Edit" buttons and simulated peer views.

### FR-4: CollaborativeTextEditor

- **FR-4.1**: Single text editing area (remove the two-pane peer simulation layout).
- **FR-4.2**: Text changes via `SpliceText` must propagate to all connected peers in real-time.
- **FR-4.3**: Display current peer count and document ID in status bar.
- **FR-4.4**: Cursor position tracking continues to work locally (no remote cursor display required).

### FR-5: CollaborativeEditor (Rich Text)

- **FR-5.1**: Single editor area (remove the two-pane peer simulation layout).
- **FR-5.2**: Text changes AND formatting marks (bold, italic, underline, link) must propagate to peers.
- **FR-5.3**: Block-level operations (split block, join block) must propagate to peers.
- **FR-5.4**: Formatting toolbar state must reflect the local cursor position (existing behavior preserved).

### FR-6: FigmaClone (Canvas)

- **FR-6.1**: Single shared canvas (remove peer simulation).
- **FR-6.2**: Shape creation, movement, resizing, and color changes must propagate to peers.
- **FR-6.3**: Layer management (add/remove/toggle visibility) must propagate to peers.
- **FR-6.4**: Shape deletion must propagate to peers.
- **FR-6.5**: Canvas must refresh from CRDT state when remote changes arrive (re-read shapes/layers).

### FR-7: TaskBoard (Kanban)

- **FR-7.1**: Add sync capability (currently has none).
- **FR-7.2**: Column creation, deletion, and renaming must propagate to peers.
- **FR-7.3**: Task creation, deletion, and cross-column moves must propagate to peers.
- **FR-7.4**: Vote counter increments must merge correctly across peers (CRDT counter).
- **FR-7.5**: Board must refresh from CRDT state when remote changes arrive.

### FR-8: App Lifecycle

- **FR-8.1**: Apps must gracefully handle SignalR disconnection and reconnection.
- **FR-8.2**: Apps must dispose of Repo, network adapter, and storage adapter on shutdown.
- **FR-8.3**: On startup, if a previously persisted document exists, the app must offer to resume it.

## Non-Functional Requirements

### NFR-1: Performance
- Sync latency must be under 500ms for typical operations (text edit, shape move, task change) on localhost.
- FileSystem storage must use incremental saves (not full snapshots on every change).

### NFR-2: Reliability
- Apps must handle server unavailability gracefully (show disconnected state, queue local changes).
- SignalR automatic reconnection must be enabled.

### NFR-3: Code Quality
- Follow existing project conventions: StyleCop, 2-space indentation, file-scoped namespaces.
- Use `[PublicAPI]` attribute on public API surfaces.
- All new services must be injectable via DI where appropriate.
- ViewModels must use ReactiveUI patterns (consistent with existing codebase).

### NFR-4: Testability
- The shared sync infrastructure (RepoService) must be testable with InMemory adapters.
- Each app must be runnable and testable via `six-avalonia-mcp` MCP tools.

### NFR-5: Developer Experience
- Running `dotnet run --project apps/Infra.AppHost` must start the full stack.
- From the Aspire dashboard, starting any demo app replica must work without manual configuration.
- Document IDs must be easily copyable between app instances for joining sessions.
