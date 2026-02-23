# Requirements: Client-Server Collaborative Demo Apps

## Overview

Refactor four collaborative Avalonia demo apps (CollaborativeTextEditor, CollaborativeEditor, CollaborativeDrawing, CollaborativeTaskBoard) from peer-to-peer architecture to client-server architecture. The `Server.Api` becomes the central point for document management and change distribution. Fix critical bugs causing UI freezes and deadlocks.

---

## Functional Requirements

### FR-1: Server-Side Document Management (Server as Repo Peer)

**FR-1.1**: Server.Api MUST run its own `Repo` instance with `FileSystemStorageAdapter` for each app-specific hub. The server acts as an always-on peer that participates in the Automerge sync protocol.

**FR-1.2**: Each demo app type MUST have its own SignalR hub endpoint on the server:
- `/hubs/text-editor` for CollaborativeTextEditor
- `/hubs/editor` for CollaborativeEditor
- `/hubs/drawing` for CollaborativeDrawing
- `/hubs/task-board` for CollaborativeTaskBoard

**FR-1.3**: Each app-specific hub MUST maintain its own `Repo` instance with isolated `FileSystemStorageAdapter` storage directory.

**FR-1.4**: Server storage directory MUST be configurable via environment variable (e.g., `SERVER_STORAGE_DIR`), defaulting to `{AppContext.BaseDirectory}/documents/`.

**FR-1.5**: When a client sends changes to the server, the server MUST:
1. Merge the changes into its own copy of the document via the Automerge sync protocol
2. Persist the merged document to its `FileSystemStorageAdapter`
3. Distribute the changes to all other connected clients via SignalR groups

**FR-1.6**: The server Repo MUST use a `SignalRServerNetworkAdapter` (new) that bridges the SignalR hub with the Repo's network subsystem, so that the server participates in the sync protocol as a first-class peer.

### FR-2: Client-Server Document Lifecycle

**FR-2.1**: When a client creates a new document:
1. The client creates the document locally via `Repo.Create<T>()`
2. The client's Automerge sync protocol sends changes to the server
3. The server merges and persists the document
4. The server distributes the document to all other connected clients

**FR-2.2**: When a client loads an existing document (Resume):
1. The client loads the document from its local `FileSystemStorageAdapter`
2. The client connects to the server and syncs via the Automerge sync protocol
3. The server sends any changes the client doesn't have
4. The client merges server changes before displaying the document to the user

**FR-2.3**: When a client joins a document by ID:
1. The client connects to the server
2. The server provides the document via the Automerge sync protocol (it has it from storage)
3. The client receives and displays the document

### FR-3: Document Discovery

**FR-3.1**: Clients MUST be able to list documents from both:
- **Local storage**: Documents previously stored on this client (current behavior via `LoadStoredDocuments`)
- **Server**: Documents available on the server that this client may not have locally

**FR-3.2**: Each app-specific hub MUST expose a `ListDocuments()` method that returns a list of document IDs available on the server for that app type.

**FR-3.3**: The `DocumentSessionDialog` MUST be updated to show both local and server-side documents, with visual distinction between:
- Documents that exist only locally
- Documents that exist on the server (and may or may not exist locally)
- Documents that exist both locally and on the server

### FR-4: Offline/Reconnection Support

**FR-4.1**: Clients MUST allow local-only editing when the server is unavailable, with a visible "disconnected" indicator.

**FR-4.2**: When a client reconnects to the server, the Automerge sync protocol MUST automatically reconcile all local changes with the server state.

**FR-4.3**: The connection status bar MUST clearly indicate connection state (Connected, Disconnected, Reconnecting).

### FR-5: Per-App Hub URL Resolution

**FR-5.1**: Each demo app MUST resolve its specific hub URL from Aspire service discovery:
- `services__api__https__0` or `services__api__http__0` + app-specific path
- Fallback to environment variable `HUB_URL`
- Fallback to hardcoded default

---

## Bug Fix Requirements

### BF-1: FileSystemStorageAdapter Deadlock

**BF-1.1**: Fix `LoadRange()` in `FileSystemStorageAdapter` which holds `_lock` (SemaphoreSlim(1,1)) and then calls `Load()` which attempts to re-acquire the same lock, causing deadlock on cold-cache scenarios.

**BF-1.2**: Fix `RemoveRange()` to clear cache entries immediately instead of relying on sliding expiration.

### BF-2: UI Thread Blocking in Repo

**BF-2.1**: Replace synchronous `_handleLock.Wait()` calls in `Repo.cs` with `await _handleLock.WaitAsync()` (or extract to internal methods that don't block the calling thread) in all methods that may be called from a UI context.

**BF-2.2**: Fix `CompositeSemaphore` in `DocHandle.cs` to use async lock acquisition instead of synchronous `.Wait()`.

**BF-2.3**: Replace synchronous `_lock.Wait()` calls in `CollectionSynchronizer.cs` and `DocSynchronizer.cs` with async alternatives to prevent UI thread blocking.

### BF-3: Demo App Event Handler Fixes

**BF-3.1**: Ensure all `async void` event handlers in demo app windows properly handle exceptions and don't freeze the UI. While `async void` is required for event handlers, ensure that long-running operations are dispatched off the UI thread.

**BF-3.2**: Add proper error handling and UI feedback for all async operations (Create, Join, Resume).

**BF-3.3**: Add `ConfigureAwait(false)` to background operations in `Repo.cs` (e.g., `ScheduleSave`, `PerformSave`) that don't need the UI synchronization context.

---

## Non-Functional Requirements

### NFR-1: Stability
- Apps MUST NOT crash or enter an unresponsive state during normal operation
- All async operations MUST have appropriate timeouts
- Network failures MUST be handled gracefully with user-visible feedback

### NFR-2: Performance
- Document sync MUST complete within 2 seconds for documents under 1MB on local network
- UI MUST remain responsive during sync operations (no blocking the UI thread)
- Server MUST handle at least 10 concurrent client connections per hub

### NFR-3: Aspire Integration
- All apps MUST work correctly when launched from `Infra.AppHost`
- Service discovery MUST work for hub URL resolution
- Health checks MUST be available for all hubs on the server

### NFR-4: Data Integrity
- Documents MUST never lose data during sync
- Server persistence MUST be durable (survive server restarts)
- The Automerge CRDT merge semantics MUST be preserved

### NFR-5: Developer Experience
- Starting `Infra.AppHost` MUST be sufficient to run the full stack
- Each app's replicas MUST be able to sync changes in real-time
- Console logging MUST be available for debugging sync operations
