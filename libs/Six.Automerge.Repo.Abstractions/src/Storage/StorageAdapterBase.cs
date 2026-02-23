namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Abstract base class for storage adapters providing common functionality.
/// </summary>
/// <remarks>
/// This base class provides a default implementation of <see cref="GetChunkInfo"/>
/// that loads the full range and extracts metadata. Derived classes can override
/// this method for more efficient implementations that don't require loading full data.
/// </remarks>
[PublicAPI]
public abstract class StorageAdapterBase : IStorageAdapter
{
  /// <inheritdoc />
  public abstract Task<byte[]?> Load(StorageKey key);

  /// <inheritdoc />
  public abstract Task Save(StorageKey key, byte[] data);

  /// <inheritdoc />
  public abstract Task Remove(StorageKey key);

  /// <inheritdoc />
  public abstract Task<IReadOnlyList<Chunk>> LoadRange(StorageKey keyPrefix);

  /// <inheritdoc />
  public abstract Task RemoveRange(StorageKey keyPrefix);

  /// <summary>
  /// Gets metadata about all chunks matching a key prefix.
  /// Default implementation loads the full range and extracts metadata.
  /// Override this method for more efficient implementations that avoid loading full data.
  /// </summary>
  /// <param name="keyPrefix">The key prefix to match.</param>
  /// <returns>Metadata for all matching chunks.</returns>
  public virtual async Task<IReadOnlyList<ChunkInfo>> GetChunkInfo(StorageKey keyPrefix)
  {
    var chunks = await LoadRange(keyPrefix);
    return chunks.Select(c => new ChunkInfo(c.Key, c.Data.Length)).ToList();
  }
}