# 2026-02-22 12:58:35 UTC | Spec: demoapp-collaborativeeditor-issues-3-the-dem | Task: sprint-1-task-7 | Sprint: Replace RichTextEditor with Plain TextBox + Storage Isolation

**Description**: Build and run the application via Aspire AppHost (`dotnet run --project infra/Infra.AppHost`). Use `six-avalonia-mcp` to verify: (a) plain TextBox renders and accepts input, (b) formatting buttons are hidden, (c) status bar still works, (d) each replica has its own storage directory.

## Sprint 1 - Replace RichTextEditor with Plain TextBox
- `RichTextEditor` was a custom UserControl overlaying a transparent TextBox + formatted TextBlock
- Replaced with a standard Avalonia `TextBox` in `EditorWindow.axaml`
- `TextBox.CaretIndexProperty`, `TextBox.SelectionStartProperty`, `TextBox.SelectionEndProperty` are the same property names but on TextBox class instead of the custom RichTextEditor
- Formatting toolbar (Bold/Italic/Underline ToggleButtons) hidden by wrapping in `StackPanel IsVisible="False"` rather than deleting — preserves AXAML named elements that may be referenced
- `REPLICA_INDEX` env var support added to `RepoService.StorageDirectory` — appends `/replica-{index}` when set
- AppHost changed from `.WithReplicas(N)` to explicit loops with individual resource names and `REPLICA_INDEX` env var
- Env var tests need save/restore pattern: capture original, set test value, assert, restore in finally block
- `dotnet build` with `--no-restore` fails if packages not already restored — always use `dotnet build` (without --no-restore) for first build in a session

# 2026-02-22 13:13:26 UTC | Spec: demoapp-collaborativeeditor-issues-3-the-dem | Task: sprint-2-task-7 | Sprint: Server-Side Document Peer Tracking

**Description**: Build Server.Api and run `dotnet test tests/Server.Api.Tests.Unit` to verify all tests pass (existing + new).

## Sprint 2 - Server-Side Document Peer Tracking (Feb 2026)
- `PeerInfoDto` added to `libs/Six.Automerge.Repo.Network.SignalR.Abstractions/src/Models/PeerInfoDto.cs` — MessagePack-serializable DTO with PeerId + optional Metadata
- `IDocumentsHubClient.OnDocumentPeersChanged(documentId, peers)` — server-to-client callback for peer presence
- `IDocumentsHub.GetDocumentPeers(documentId)` and `GetDocumentPeerCount(documentId)` — client-to-server RPC
- `DocumentPeerTracker` lives in `apps/Server.Api/src/Services/DocumentPeerTracker.cs` — singleton, uses ConcurrentDictionary for doc→peers and reverse index peer→docs with lock on HashSet
- Hub broadcasts peer changes: OthersInGroup gets full list, Caller gets list excluding self
- Relay-mode hub in `libs/Six.Automerge.Repo.Network.SignalR.Server/` also implements IDocumentsHub but returns empty lists for peer tracking (no tracker instance)
- `SignalRNetworkAdapter` has a no-op `OnDocumentPeersChanged` — future sprint will wire client-side handling
- Server.Api.Tests.Unit: 71 tests passing (41 existing + 20 DocumentPeerTracker + 10 new hub peer tracking)
- When adding methods to `IDocumentsHub` or `IDocumentsHubClient`, must update ALL implementors: Server.Api/Hubs/DocumentsHub, SignalR.Server/DocumentsHub, SignalR.Client/SignalRNetworkAdapter

# 2026-02-22 14:21:53 UTC | Spec: demoapp-collaborativeeditor-issues-3-the-dem | Task: sprint-3-task-6 | Sprint: Client-Side Peer Count Integration + End-to-End Testing

**Description**: Build and run the full stack via Aspire AppHost. Use `six-avalonia-mcp` to verify end-to-end: start 3 replicas, create a document on replica-0, join from replica-1 and replica-2. Verify that all clients show correct peer counts (replica-0 shows "Syncing (2 peers)", etc.). Verify typing syncs correctly across all replicas. Verify that when a replica is closed, other replicas' peer counts decrease.

Sprint 3 E2E: Peer count system includes server's own peer in the count. When 2 client replicas are on a document, each client sees "Peers: 2" (other client + server peer). The server's DocumentPeerTracker tracks all peers including the server's SyncServer peer. The `\n` in simulate_type is typed as literal backslash+n in single-line TextBox, not as newline.

