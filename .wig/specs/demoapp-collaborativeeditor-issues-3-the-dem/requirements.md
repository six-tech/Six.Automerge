# Requirements: DemoApp.CollaborativeEditor Issues 3

## Overview

Three distinct issues need to be fixed in the CollaborativeEditor demo app:

1. **RichTextEditor replacement** — The custom `RichTextEditor` control (dual TextBox+TextBlock overlay) has broken selection rendering. Replace with a plain `TextBox` while keeping formatting classes/toolbar hidden for future use.
2. **Storage isolation per replica** — All Aspire replicas share the same storage directory (`~/.rayautomerge/CollaborativeEditor/`), causing crashes when multiple instances read/write the same document files concurrently.
3. **Peer count bug** — Each client only sees the server as its single peer (PeerCount = 1). Need server-side peer tracking so clients can see the actual count and info of OTHER clients editing the same document.

---

## Functional Requirements

### FR-1: Replace RichTextEditor with Plain TextBox

- **FR-1.1**: Replace the `RichTextEditor` UserControl in `EditorWindow.axaml` with a standard Avalonia `TextBox`.
- **FR-1.2**: The plain TextBox must support multiline text (`AcceptsReturn=True`, `TextWrapping=Wrap`).
- **FR-1.3**: The `DocumentText` two-way binding must work with the plain TextBox.
- **FR-1.4**: Caret position and selection tracking must work correctly with the plain TextBox (updating `CursorPosition`, `SelectionLength` in EditorViewModel).
- **FR-1.5**: The formatting toolbar buttons (Bold, Italic, Underline) must be hidden (`IsVisible=False`) but remain in AXAML.
- **FR-1.6**: The keyboard shortcuts (Ctrl+B/I/U) must be disabled or no-op.
- **FR-1.7**: Keep `FormattedTextRenderer.cs`, `FormattingState.cs`, `RichTextEditor.axaml/.cs` files in the project for future use. They should still compile.
- **FR-1.8**: The `FormattedText` property on `EditorViewModel` can remain but is not bound to the UI.
- **FR-1.9**: The SyncStatus text and bottom status bar (cursor position, selection length, char count) must continue working.
- **FR-1.10**: Window KeyBindings for Ctrl+B/I/U should be removed (or the commands should be no-ops).

### FR-2: Per-Replica Storage Isolation

- **FR-2.1**: Each Aspire replica of CollaborativeEditor must have its own isolated storage directory.
- **FR-2.2**: Storage path format: `~/.rayautomerge/CollaborativeEditor/replica-{index}/` where `{index}` is 0, 1, 2, etc.
- **FR-2.3**: The AppHost must inject a `REPLICA_INDEX` environment variable into each replica.
- **FR-2.4**: Since `.WithReplicas(N)` does not support per-replica env vars, the AppHost must create individual project resources in a loop instead.
- **FR-2.5**: `RepoService` must read the `REPLICA_INDEX` env var and incorporate it into the `StorageDirectory` path.
- **FR-2.6**: When `REPLICA_INDEX` is not set (e.g., running standalone), behavior must be unchanged — use the default path without a replica subdirectory.
- **FR-2.7**: Each replica must be able to Create, Join, and Resume documents independently without file contention.

### FR-3: Server-Side Peer Tracking & Client Display

- **FR-3.1**: The server must track which peers are subscribed to which documents.
- **FR-3.2**: A new hub method `GetDocumentPeers(string documentId)` must return a list of peer info objects for all clients subscribed to that document (excluding the server itself and the caller).
- **FR-3.3**: A new hub method `GetDocumentPeerCount(string documentId)` must return the number of other clients subscribed to that document (excluding server and caller).
- **FR-3.4**: A new client callback `OnDocumentPeersChanged(string documentId, List<PeerInfoDto> peers)` must notify clients when peers join/leave a document group.
- **FR-3.5**: `PeerInfoDto` must include at minimum: `PeerId` (string), and optionally `Metadata` (for future display name, etc.).
- **FR-3.6**: The client `ConnectionStatusBar` must display the count of OTHER clients editing the same document, not the Repo-level peer count.
- **FR-3.7**: The `EditorViewModel.SyncStatus` must reflect the real peer count (e.g., "Syncing (2 peers)" means 2 other clients are connected).
- **FR-3.8**: Peer count must update in real-time as clients join/leave the document group.
- **FR-3.9**: On initial document open/join, the client must query the current peer list to set the initial count.

---

## Non-Functional Requirements

- **NFR-1**: All existing tests (1,083+) must continue to pass after changes.
- **NFR-2**: New functionality must have unit test coverage.
- **NFR-3**: Changes to `IDocumentsHub` and `IDocumentsHubClient` interfaces must maintain backward compatibility (new methods are additive).
- **NFR-4**: No blocking calls on the UI thread — all SignalR calls and file I/O must be async.
- **NFR-5**: Thread-safe peer tracking on the server using concurrent collections.
- **NFR-6**: The solution must work with the Aspire AppHost orchestration (Infra.AppHost) and be testable via `six-avalonia-mcp`.
