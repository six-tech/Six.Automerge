namespace Six.Automerge.Repository.Storage;

/// <summary>
/// In-memory storage adapter for testing and ephemeral repositories.
/// </summary>
/// <remarks>
/// This adapter stores all data in memory using a dictionary. Data is lost when the adapter
/// is disposed or when the application terminates. Use this adapter for:
/// <list type="bullet">
/// <item>Unit and integration testing</item>
/// <item>Temporary documents that don't require persistence</item>
/// <item>Prototyping and development</item>
/// </list>
/// <para>
/// This adapter is thread-safe and supports concurrent operations.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class InMemoryStorageAdapter : IStorageAdapter, IDisposable
{
  readonly Dictionary<string, byte[]> _storage = [];
  readonly SemaphoreSlim _lock = new(1, 1);
  bool _isDisposed;
  
  
  /// <summary>
  /// Gets the number of keys currently stored.
  /// </summary>
  /// <remarks>
  /// This property is useful for testing and debugging.
  /// </remarks>
  public int Count
  {
    get
    {
      ThrowIfDisposed();
      return _storage.Count;
    }
  }
  
  
  
  /// <summary>
  /// 
  /// Disposes this storage adapter and clears all data.
  /// </summary>
  public void Dispose()
  {
    if(_isDisposed)
      return;

    _isDisposed = true;
    _storage.Clear();
    _lock.Dispose();
  }

  
  public async Task<byte[]?> Load(StorageKey key)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      var keyString = key.ToDebugString();
      if(!_storage.TryGetValue(keyString, out var data))
        return null;

      // Copy the data to ensure isolation
      var copy = new byte[data.Length];
      Array.Copy(data, copy, data.Length);
      return copy;
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task Save(StorageKey key, byte[] data)
  {
    ThrowIfDisposed();

    if(data is null)
      throw new ArgumentNullException(nameof(data));

    await _lock.WaitAsync();
    try
    {
      var keyString = key.ToDebugString();
      // Copy the data to ensure isolation
      var copy = new byte[data.Length];
      Array.Copy(data, copy, data.Length);
      _storage[keyString] = copy;
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task Remove(StorageKey key)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      var keyString = key.ToDebugString();
      _storage.Remove(keyString);
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task<IReadOnlyList<Chunk>> LoadRange(StorageKey keyPrefix)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      var results = new List<Chunk>();

      foreach(var kvp in _storage)
      {
        // Parse the key back to StorageKey
        var components = kvp.Key.Split('/');
        var storageKey = new StorageKey(components);

        // Check if it matches the prefix
        if(storageKey.StartsWith(keyPrefix))
        {
          // Copy the data to ensure isolation
          var dataCopy = new byte[kvp.Value.Length];
          Array.Copy(kvp.Value, dataCopy, kvp.Value.Length);
          results.Add(new Chunk(storageKey, dataCopy));
        }
      }

      return results;
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task RemoveRange(StorageKey keyPrefix)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      // FindAsync all keys that match the prefix
      var keysToRemove = new List<string>();

      foreach(var kvp in _storage)
      {
        var components = kvp.Key.Split('/');
        var storageKey = new StorageKey(components);

        if(storageKey.StartsWith(keyPrefix))
        {
          keysToRemove.Add(kvp.Key);
        }
      }

      // Remove them
      foreach(var key in keysToRemove)
      {
        _storage.Remove(key);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  public async Task<IReadOnlyList<ChunkInfo>> GetChunkInfo(StorageKey keyPrefix)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      var results = new List<ChunkInfo>();

      foreach(var kvp in _storage)
      {
        var components = kvp.Key.Split('/');
        var storageKey = new StorageKey(components);

        if(storageKey.StartsWith(keyPrefix))
        {
          results.Add(new ChunkInfo(storageKey, kvp.Value.Length));
        }
      }

      return results;
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Clears all data from storage.
  /// </summary>
  /// <remarks>
  /// This method is useful for testing scenarios where you need to reset storage state.
  /// </remarks>
  public async Task Clear()
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      _storage.Clear();
    }
    finally
    {
      _lock.Release();
    }
  }

  
  
  void ThrowIfDisposed()
  {
    if(_isDisposed)
      throw new ObjectDisposedException(nameof(InMemoryStorageAdapter));
  }

}