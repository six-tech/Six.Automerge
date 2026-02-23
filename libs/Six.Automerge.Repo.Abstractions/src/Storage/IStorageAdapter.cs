namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Interface for storage adapters that persist Automerge documents and sync state.
/// Implementations can target any backend: memory, file system, database, cloud storage, etc.
/// </summary>
/// <remarks>
/// <para>
/// Storage adapters must be thread-safe and support concurrent operations.
/// Multiple calls to the same or different keys may occur simultaneously.
/// </para>
/// <para>
/// Keys use a hierarchical structure (e.g., [documentId, "snapshot", hash]) to organize data.
/// Use <see cref="StorageKey"/> factory methods to create properly formatted keys.
/// </para>
/// <example>
/// Example implementation using in-memory dictionary:
/// <code>
/// public class InMemoryAdapter : IStorageAdapter
/// {
///     private readonly Dictionary&lt;string, byte[]&gt; _storage = new();
///
///     public Task&lt;byte[]?&gt; Load(StorageKey key)
///     {
///         var keyStr = key.ToDebugString();
///         return Task.FromResult(_storage.TryGetValue(keyStr, out var data) ? data : null);
///     }
///
///     // ... implement other methods
/// }
/// </code>
/// </example>
/// </remarks>
[PublicAPI]
public interface IStorageAdapter
{
    /// <summary>
    /// Loads data for a single storage key.
    /// </summary>
    /// <param name="key">The storage key to load.</param>
    /// <returns>The data if found; null if the key does not exist.</returns>
    /// <remarks>
    /// This method should return null (not throw an exception) when the key is not found.
    /// </remarks>
    Task<byte[]?> Load(StorageKey key);

    /// <summary>
    /// Saves data for a single storage key.
    /// </summary>
    /// <param name="key">The storage key to save.</param>
    /// <param name="data">The data to save.</param>
    /// <remarks>
    /// If the key already exists, its data should be replaced.
    /// Implementations should copy the data array if they need to retain it beyond the async operation.
    /// </remarks>
    Task Save(StorageKey key, byte[] data);

    /// <summary>
    /// Removes data for a single storage key.
    /// </summary>
    /// <param name="key">The storage key to remove.</param>
    /// <remarks>
    /// This method should succeed silently if the key does not exist.
    /// </remarks>
    Task Remove(StorageKey key);

    /// <summary>
    /// Loads all chunks matching a key prefix (e.g., all chunks for a document).
    /// </summary>
    /// <param name="keyPrefix">The key prefix to match.</param>
    /// <returns>All matching chunks. Returns an empty list if no matches are found.</returns>
    /// <remarks>
    /// Use <see cref="StorageKey.StartsWith"/> to determine if a key matches the prefix.
    /// For example, prefix [documentId] matches [documentId, "snapshot", hash] and [documentId, "incremental", hash].
    /// </remarks>
    /// <example>
    /// <code>
    /// var prefix = new StorageKey(new[] { documentId });
    /// var chunks = await adapter.LoadRange(prefix);
    /// // Returns all chunks with keys starting with [documentId]
    /// </code>
    /// </example>
    Task<IReadOnlyList<Chunk>> LoadRange(StorageKey keyPrefix);

    /// <summary>
    /// Removes all keys matching a key prefix.
    /// </summary>
    /// <param name="keyPrefix">The key prefix to match.</param>
    /// <remarks>
    /// Use <see cref="StorageKey.StartsWith"/> to determine if a key matches the prefix.
    /// This method should succeed silently if no matching keys are found.
    /// </remarks>
    Task RemoveRange(StorageKey keyPrefix);

    /// <summary>
    /// Gets metadata about all chunks matching a key prefix (without loading data).
    /// Used for storage optimization decisions.
    /// </summary>
    /// <param name="keyPrefix">The key prefix to match.</param>
    /// <returns>Metadata for all matching chunks. Returns an empty list if no matches are found.</returns>
    /// <remarks>
    /// This method is used by the storage subsystem to determine if compaction is needed.
    /// Implementations can optimize this by returning size metadata without loading full chunk data.
    /// </remarks>
    Task<IReadOnlyList<ChunkInfo>> GetChunkInfo(StorageKey keyPrefix);
}
