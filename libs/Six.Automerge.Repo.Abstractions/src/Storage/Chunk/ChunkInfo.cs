namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Metadata about a stored chunk (without loading the data).
/// Used for storage optimization decisions.
/// </summary>
[PublicAPI]
public sealed record ChunkInfo(StorageKey Key, int Size)
{
  /// <summary>
  /// Gets the type of this chunk based on its storage key.
  /// </summary>
  public ChunkType Type => Chunk.ChunkTypeFromKey(Key);
}