using Microsoft.Extensions.Caching.Memory;
using Six.Automerge.Repository.Storage;

namespace Six.Automerge.Repository.Storage.FileSystem;

/// <summary>
/// File system storage adapter that persists data to local disk with in-memory caching.
/// </summary>
/// <remarks>
/// <para>
/// This adapter stores data in a hierarchical directory structure with sharding to prevent
/// directory overflow. The first key component is split: first 2 chars become a directory,
/// remaining chars become a subdirectory.
/// </para>
/// <para>
/// Example: Key ["abc123", "snapshot", "hash"] → baseDir/ab/c123/snapshot/hash
/// </para>
/// <para>
/// Data is cached in memory using MemoryCache for fast repeated reads.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class FileSystemStorageAdapter : StorageAdapterBase, IDisposable
{
  readonly string _baseDirectory;
  readonly IMemoryCache _cache;
  readonly HashSet<string> _cacheKeys = [];
  readonly SemaphoreSlim _lock = new(1, 1);
  readonly TimeSpan _cacheSlidingExpiration;
  bool _disposed;


  /// <summary>
  /// Creates a new file system storage adapter.
  /// </summary>
  /// <param name="baseDirectory">Base directory for storage. Created if it doesn't exist.</param>
  /// <param name="options">Optional configuration. If null, uses default settings.</param>
  public FileSystemStorageAdapter(string baseDirectory, FileSystemStorageOptions? options = null)
  {
    if(string.IsNullOrWhiteSpace(baseDirectory))
      throw new ArgumentException("Base directory cannot be null or whitespace", nameof(baseDirectory));

    options ??= new FileSystemStorageOptions();

    _baseDirectory = Path.GetFullPath(baseDirectory);
    _cacheSlidingExpiration = options.CacheSlidingExpiration;

    // Initialize cache with size limit
    _cache = new MemoryCache(new MemoryCacheOptions
    {
      SizeLimit = options.CacheSizeLimit
    });

    // Create base directory if it doesn't exist
    Directory.CreateDirectory(_baseDirectory);
  }


  /// <summary>
  /// Loads data for a storage key. Checks cache first, then disk.
  /// </summary>
  public override async Task<byte[]?> Load(StorageKey key)
  {
    ThrowIfDisposed();

    await _lock.WaitAsync();
    try
    {
      return await LoadCore(key);
    }
    finally
    {
      _lock.Release();
    }
  }


  /// <summary>
  /// Loads data for a storage key without acquiring the lock.
  /// Checks cache first, then reads from disk and populates cache.
  /// </summary>
  /// <remarks>
  /// Callers must hold <see cref="_lock"/> before calling this method.
  /// This exists so <see cref="LoadRange"/> can call it from within its own lock scope
  /// without causing a deadlock.
  /// </remarks>
  async Task<byte[]?> LoadCore(StorageKey key)
  {
    var cacheKey = GetCacheKey(key);

    // Try cache first
    if(_cache.TryGetValue(cacheKey, out byte[]? cachedData))
    {
      return cachedData;
    }

    // Load from disk
    var filePath = GetFilePath(key);

    try
    {
      if(!File.Exists(filePath))
        return null;

      var data = await File.ReadAllBytesAsync(filePath);

      // Store in cache with sliding expiration
      var cacheOptions = new MemoryCacheEntryOptions()
        .SetSize(data.Length)
        .SetSlidingExpiration(_cacheSlidingExpiration);

      _cache.Set(cacheKey, data, cacheOptions);

      // Track cache key for prefix-based eviction
      _cacheKeys.Add(cacheKey);

      return data;
    }
    catch(FileNotFoundException)
    {
      return null;
    }
    catch(DirectoryNotFoundException)
    {
      return null;
    }
  }

  /// <summary>
  /// Saves data to storage. Updates cache and writes to disk.
  /// </summary>
  public override async Task Save(StorageKey key, byte[] data)
  {
    ThrowIfDisposed();

    if(data == null)
      throw new ArgumentNullException(nameof(data));

    var cacheKey = GetCacheKey(key);
    var filePath = GetFilePath(key);

    await _lock.WaitAsync();
    try
    {
      // Create directory structure
      var directory = Path.GetDirectoryName(filePath);
      if(!string.IsNullOrEmpty(directory))
      {
        Directory.CreateDirectory(directory);
      }

      // Write to disk
      await File.WriteAllBytesAsync(filePath, data);

      // Update cache
      var cacheOptions = new MemoryCacheEntryOptions()
        .SetSize(data.Length)
        .SetSlidingExpiration(_cacheSlidingExpiration);

      _cache.Set(cacheKey, data, cacheOptions);

      // Track cache key for prefix-based eviction
      _cacheKeys.Add(cacheKey);
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Removes data for a storage key. Removes from cache and deletes file.
  /// </summary>
  public override async Task Remove(StorageKey key)
  {
    ThrowIfDisposed();

    var cacheKey = GetCacheKey(key);
    var filePath = GetFilePath(key);

    await _lock.WaitAsync();
    try
    {
      // Remove from cache and tracking set
      _cache.Remove(cacheKey);
      _cacheKeys.Remove(cacheKey);

      // Delete file (ignore if doesn't exist)
      if(File.Exists(filePath))
      {
        File.Delete(filePath);
      }
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Loads all chunks matching a key prefix. Combines cached and disk data.
  /// </summary>
  public override async Task<IReadOnlyList<Chunk>> LoadRange(StorageKey keyPrefix)
  {
    ThrowIfDisposed();

    var dirPath = keyPrefix.Components.Count == 0 ? _baseDirectory : GetFilePath(keyPrefix);
    var results = new Dictionary<string, Chunk>(); // Use dict to deduplicate

    await _lock.WaitAsync();
    try
    {
      // Walk directory tree to find all files
      var diskFiles = await WalkDirectory(dirPath);

      // Load each file (will hit cache if available)
      foreach(var filePath in diskFiles)
      {
        // Convert file path back to StorageKey
        var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        var components = ParseFilePathToKey(relativePath);
        var storageKey = new StorageKey(components);

        // Check if key matches prefix
        if(storageKey.StartsWith(keyPrefix))
        {
          // Load data without re-acquiring lock (hits cache if present)
          var data = await LoadCore(storageKey);
          if(data != null)
          {
            var keyString = storageKey.ToDebugString();
            results[keyString] = new Chunk(storageKey, data);
          }
        }
      }

      return results.Values.ToList();
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Removes all keys matching a prefix. Deletes directory tree and clears from cache.
  /// </summary>
  public override async Task RemoveRange(StorageKey keyPrefix)
  {
    ThrowIfDisposed();

    var dirPath = GetFilePath(keyPrefix);

    await _lock.WaitAsync();
    try
    {
      // Delete directory tree from disk
      if(Directory.Exists(dirPath))
      {
        Directory.Delete(dirPath, recursive: true);
      }

      // Evict all cache entries matching the prefix
      var prefixString = keyPrefix.ToDebugString();
      var keysToRemove = _cacheKeys.Where(k => k.StartsWith(prefixString, StringComparison.Ordinal)).ToList();
      foreach(var cacheKey in keysToRemove)
      {
        _cache.Remove(cacheKey);
        _cacheKeys.Remove(cacheKey);
      }
    }
    catch(DirectoryNotFoundException)
    {
      // Directory already deleted, ignore
    }
    finally
    {
      _lock.Release();
    }
  }

  /// <summary>
  /// Gets metadata about all chunks matching a key prefix without loading the full data.
  /// This is more efficient than the base implementation as it only reads file metadata.
  /// </summary>
  public override async Task<IReadOnlyList<ChunkInfo>> GetChunkInfo(StorageKey keyPrefix)
  {
    ThrowIfDisposed();

    var dirPath = keyPrefix.Components.Count == 0 ? _baseDirectory : GetFilePath(keyPrefix);
    var results = new List<ChunkInfo>();

    await _lock.WaitAsync();
    try
    {
      // Walk directory tree to find all files
      var diskFiles = await WalkDirectory(dirPath);

      // Get metadata for each file
      foreach(var filePath in diskFiles)
      {
        // Convert file path back to StorageKey
        var relativePath = Path.GetRelativePath(_baseDirectory, filePath);
        var components = ParseFilePathToKey(relativePath);
        var storageKey = new StorageKey(components);

        // Check if key matches prefix
        if(storageKey.StartsWith(keyPrefix))
        {
          // Get file size without loading data
          var fileInfo = new FileInfo(filePath);
          if(fileInfo.Exists)
          {
            results.Add(new ChunkInfo(storageKey, (int)fileInfo.Length));
          }
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
  /// Converts a storage key to a file system path using directory sharding.
  /// </summary>
  /// <remarks>
  /// The first key component is split into two parts for sharding:
  /// - First 2 characters become a directory (prevents too many files in one dir)
  /// - Remaining characters become a subdirectory
  /// - Other components are appended as subdirectories
  ///
  /// Example: ["abc123def", "snapshot", "hash456"]
  /// → baseDir/ab/c123def/snapshot/hash456
  /// </remarks>
  string GetFilePath(StorageKey key)
  {
    if(key.Components.Count == 0)
      throw new ArgumentException("StorageKey must have at least one component");

    var firstKey = key.Components[0];
    if(firstKey.Length < 2)
      throw new ArgumentException($"First key component must be at least 2 chars: {firstKey}");

    // Shard: first 2 chars / remaining chars / other components
    var shardDir = firstKey[..2];         // First 2 chars
    var remaining = firstKey[2..];         // Rest of first key

    var pathComponents = new List<string> { _baseDirectory, shardDir, remaining };
    pathComponents.AddRange(key.Components.Skip(1));

    return Path.Combine(pathComponents.ToArray());
  }


  /// <summary>
  /// Gets the cache key for a storage key (uses ToDebugString for consistency).
  /// </summary>
  string GetCacheKey(StorageKey key) => key.ToDebugString();


  /// <summary>
  /// Converts a relative file path back to storage key components.
  /// Reverses the sharding: ab/c123/snapshot/hash → ["abc123", "snapshot", "hash"]
  /// </summary>
  List<string> ParseFilePathToKey(string relativePath)
  {
    var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    if(parts.Length < 2)
      throw new ArgumentException($"Invalid file path format: {relativePath}");

    // Reconstruct first key: parts[0] + parts[1] (un-shard)
    var firstKey = parts[0] + parts[1];

    var components = new List<string> { firstKey };
    components.AddRange(parts.Skip(2));

    return components;
  }


  /// <summary>
  /// Recursively walks a directory and returns all file paths.
  /// Returns empty list if directory doesn't exist.
  /// </summary>
  async Task<List<string>> WalkDirectory(string dirPath)
  {
    var files = new List<string>();

    if(!Directory.Exists(dirPath))
      return files;

    try
    {
      // Get all files in current directory
      var directFiles = Directory.GetFiles(dirPath);
      files.AddRange(directFiles);

      // Recursively process subdirectories
      var subdirectories = Directory.GetDirectories(dirPath);
      foreach(var subdir in subdirectories)
      {
        var subFiles = await WalkDirectory(subdir);
        files.AddRange(subFiles);
      }
    }
    catch(UnauthorizedAccessException)
    {
      // Skip directories we can't access
    }
    catch(DirectoryNotFoundException)
    {
      // Directory was deleted during traversal
    }

    return files;
  }


  /// <summary>
  /// Disposes the adapter and releases resources.
  /// </summary>
  public void Dispose()
  {
    if(_disposed)
      return;

    _disposed = true;

    _cache.Dispose();
    _lock.Dispose();
  }


  /// <summary>
  /// Throws if the adapter has been disposed.
  /// </summary>
  void ThrowIfDisposed()
  {
    if(_disposed)
      throw new ObjectDisposedException(nameof(FileSystemStorageAdapter));
  }
}


/// <summary>
/// Configuration options for FileSystemStorageAdapter.
/// </summary>
[PublicAPI]
public sealed class FileSystemStorageOptions
{
  /// <summary>
  /// Maximum cache size in bytes. Default: 100 MB.
  /// </summary>
  public long CacheSizeLimit { get; init; } = 100 * 1024 * 1024;

  /// <summary>
  /// Sliding expiration time for cached entries. Default: 5 minutes.
  /// </summary>
  public TimeSpan CacheSlidingExpiration { get; init; } = TimeSpan.FromMinutes(5);
}
