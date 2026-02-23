## v1 — 2026-02-20
Initial creation from run observation.

Interoptopus codegen forbids parameter name 'value' to ensure cross-language compatibility

## v2 — 2026-02-20
Reinforced with new observation.

Helper functions in types.rs require pub visibility for cross-module use; Vec types need builtins_vec! registration

## v3 — 2026-02-20
Reinforced with new observation.

AutomergeScalarValue constructed with typed helpers; 'value' parameter name forbidden

## v4 — 2026-02-20
Reinforced with new observation.

List operations use usize indices; Cursor serialized as ByteArray; InternalsVisibleTo needed for test access

## v5 — 2026-02-20
Reinforced with new observation.

Multiple FFI functions for historical queries; ObjId is ExId re-export; 'value' parameter forbidden

## v6 — 2026-02-20
Reinforced with new observation.

Save/load use builder pattern; diff_incremental and has_our_changes are native; get_missing_deps with empty heads returns empty

## v7 — 2026-02-20
Reinforced with new observation.

Starlight sidebar uses slug keys, internal links need /Six.Automerge/ base prefix, future sprint directories pre-created

## v8 — 2026-02-20
Reinforced with new observation.

Build and verify Server.Api with SignalR hub and Aspire dashboard startup

## v9 — 2026-02-21
Reinforced with new observation.

Fixed FileSystemStorageAdapter deadlock by extracting lock-free LoadCore(); fixed cache staleness by tracking and evicting keys in RemoveRange()

## v10 — 2026-02-21
Reinforced with new observation.

Convert synchronous Handles property to async; call RegisterHandle outside locks to break deadlock chain through synchronizer callbacks

## v11 — 2026-02-21
Reinforced with new observation.

Demo apps must dispatch post-await ViewModel updates to UI thread; use 30s timeout on WhenReadyAsync to prevent hang

## v12 — 2026-02-21
Reinforced with new observation.

Centralize URL resolution in RepoService; make HubPath required per-app, optional HubUrl override

## v13 — 2026-02-21
Reinforced with new observation.

Clean stale cargo targets and rebuild after crate rename; lib prefix required on all platforms including Windows

## v14 — 2026-02-21
Reinforced with new observation.

New native build scripts with platform-aware artifact directories and incremental tracking via JSON state file

## v15 — 2026-02-21
Reinforced with new observation.

GitHub Actions workflow handles multi-platform Rust builds with artifact restructuring for NuGet packaging

## v16 — 2026-02-21
Reinforced with new observation.

Structured logging added to DocSynchronizer and CollectionSynchronizer via constructor injection

## v17 — 2026-02-21
Reinforced with new observation.

Nested lock acquisition prevented by moving callback outside critical section and making async

## v18 — 2026-02-21
Reinforced with new observation.

Test helper wraps synchronous actions as async delegates for compatibility

## v19 — 2026-02-21
Reinforced with new observation.

Interlocked operations for thread-safe flags; structured logging added throughout UI layer

## v20 — 2026-02-22
Reinforced with new observation.

Replaced RichTextEditor with standard TextBox; added REPLICA_INDEX env var for isolated storage per replica

## v21 — 2026-02-22
Reinforced with new observation.

Implemented server-side peer tracking via PeerInfoDto and DocumentPeerTracker singleton with hub broadcasting

## v22 — 2026-02-22
Reinforced with new observation.

Use volatile _processingLocal flag to suppress spurious RemoteRefresh writes in channel queue

## v23 — 2026-02-22
Reinforced with new observation.

Pattern: use IHubContext<THubType, TClientInterface> injected via DI to broadcast from outside hubs

