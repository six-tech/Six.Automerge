namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Represents a chunk of data retrieved from storage.
/// </summary>
[PublicAPI]
public sealed record Chunk(StorageKey Key, byte[] Data)
{
  /// <summary>
  /// Gets the type of this chunk based on its storage key.
  /// </summary>
  public ChunkType Type => ChunkTypeFromKey(Key);

  /// <summary>
  /// Determines the chunk type from a storage key.
  /// </summary>
  /// <param name="key">The storage key to analyze.</param>
  /// <returns>The chunk type.</returns>
  /// <exception cref="ArgumentException">Thrown if the key format is invalid.</exception>
  public static ChunkType ChunkTypeFromKey(StorageKey key)
  {
    if(key.Components == null || key.Components.Count < 2)
    {
      throw new ArgumentException("Invalid storage key format: must have at least 2 components", nameof(key));
    }

    return key.Components[1] switch
    {
      "snapshot" => ChunkType.Snapshot,
      "incremental" => ChunkType.Incremental,
      "sync-state" => ChunkType.SyncState,
      _ => throw new ArgumentException($"Unknown chunk type: {key.Components[1]}", nameof(key))
    };
  }
}