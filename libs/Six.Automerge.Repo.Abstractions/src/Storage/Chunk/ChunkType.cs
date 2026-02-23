namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Type of chunk stored in storage.
/// </summary>
[PublicAPI]
public enum ChunkType
{
  /// <summary>Full document snapshot.</summary>
  Snapshot,

  /// <summary>Incremental changes since a previous state.</summary>
  Incremental,

  /// <summary>Sync state for a peer connection.</summary>
  SyncState
}