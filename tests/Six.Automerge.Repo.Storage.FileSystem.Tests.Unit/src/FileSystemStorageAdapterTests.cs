using FluentAssertions;
using Six.Automerge.Repository.Storage;
using Six.Automerge.Repository.Storage.FileSystem;
using Xunit;

namespace Six.Automerge.Repo.Storage.FileSystem.Tests;

/// <summary>
/// Unit tests for FileSystemStorageAdapter.
/// </summary>
public sealed class FileSystemStorageAdapterTests : IDisposable
{
  readonly string _testDirectory;

  public FileSystemStorageAdapterTests()
  {
    // Create a unique temporary directory for each test
    _testDirectory = Path.Combine(Path.GetTempPath(), "automerge-fs-tests", Guid.NewGuid().ToString());
  }

  public void Dispose()
  {
    // Clean up test directory
    if(Directory.Exists(_testDirectory))
    {
      try
      {
        Directory.Delete(_testDirectory, recursive: true);
      }
      catch
      {
        // Ignore cleanup errors
      }
    }
  }


  [Fact]
  public void Constructor_CreatesBaseDirectory()
  {
    // Arrange
    var dir = Path.Combine(_testDirectory, "new-dir");

    // Act
    using var adapter = new FileSystemStorageAdapter(dir);

    // Assert
    Directory.Exists(dir).Should().BeTrue("constructor should create base directory");
  }


  [Fact]
  public void Constructor_ThrowsOnNullOrWhitespaceDirectory()
  {
    // Act & Assert
    var act1 = () => new FileSystemStorageAdapter(null!);
    act1.Should().Throw<ArgumentException>();

    var act2 = () => new FileSystemStorageAdapter("");
    act2.Should().Throw<ArgumentException>();

    var act3 = () => new FileSystemStorageAdapter("   ");
    act3.Should().Throw<ArgumentException>();
  }


  [Fact]
  public async Task Save_ThenLoad_ReturnsSameData()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["abc123", "snapshot", "hash456"]);
    var data = "Hello World"u8.ToArray();

    // Act
    await adapter.Save(key, data);
    var loaded = await adapter.Load(key);

    // Assert
    loaded.Should().NotBeNull();
    loaded.Should().BeEquivalentTo(data);
  }


  [Fact]
  public async Task Load_NonExistentKey_ReturnsNull()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["nonexistent", "key"]);

    // Act
    var result = await adapter.Load(key);

    // Assert
    result.Should().BeNull();
  }


  [Fact]
  public async Task Save_CreatesDirectoryStructure()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["abc123", "snapshot", "hash456"]);
    var data = "test"u8.ToArray();

    // Act
    await adapter.Save(key, data);

    // Assert
    // Expected path: baseDir/ab/c123/snapshot/hash456
    var expectedPath = Path.Combine(_testDirectory, "ab", "c123", "snapshot", "hash456");
    File.Exists(expectedPath).Should().BeTrue("file should exist at sharded path");
  }


  [Fact]
  public async Task Remove_DeletesFile()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["abc123", "snapshot", "hash456"]);
    var data = "test"u8.ToArray();
    await adapter.Save(key, data);

    // Act
    await adapter.Remove(key);

    // Assert
    var loaded = await adapter.Load(key);
    loaded.Should().BeNull("removed key should not be loadable");
  }


  [Fact]
  public async Task Remove_NonExistentKey_DoesNotThrow()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["nonexistent", "key"]);

    // Act & Assert
    var act = async () => await adapter.Remove(key);
    await act.Should().NotThrowAsync("removing non-existent key should be idempotent");
  }


  [Fact]
  public async Task LoadRange_ReturnsAllMatchingKeys()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var docId = "doc123";

    // Save multiple chunks for same document
    await adapter.Save(new StorageKey([docId, "snapshot", "hash1"]), "snap1"u8.ToArray());
    await adapter.Save(new StorageKey([docId, "incremental", "hash2"]), "inc1"u8.ToArray());
    await adapter.Save(new StorageKey([docId, "incremental", "hash3"]), "inc2"u8.ToArray());

    // Save chunk for different document (should not be returned)
    await adapter.Save(new StorageKey(["other-doc", "snapshot", "hash4"]), "other"u8.ToArray());

    // Act
    var prefix = new StorageKey([docId]);
    var chunks = await adapter.LoadRange(prefix);

    // Assert
    chunks.Should().HaveCount(3, "should return all chunks for doc123");
    chunks.Should().Contain(c => c.Key.Components[1] == "snapshot");
    chunks.Should().Contain(c => c.Key.Components[1] == "incremental" && c.Key.Components[2] == "hash2");
    chunks.Should().Contain(c => c.Key.Components[1] == "incremental" && c.Key.Components[2] == "hash3");
  }


  [Fact]
  public async Task LoadRange_EmptyDirectory_ReturnsEmptyList()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var prefix = new StorageKey(["nonexistent"]);

    // Act
    var chunks = await adapter.LoadRange(prefix);

    // Assert
    chunks.Should().BeEmpty();
  }


  [Fact]
  public async Task RemoveRange_DeletesDirectoryTree()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var docId = "doc123";

    await adapter.Save(new StorageKey([docId, "snapshot", "hash1"]), "snap1"u8.ToArray());
    await adapter.Save(new StorageKey([docId, "incremental", "hash2"]), "inc1"u8.ToArray());
    await adapter.Save(new StorageKey([docId, "incremental", "hash3"]), "inc2"u8.ToArray());

    // Act
    var prefix = new StorageKey([docId]);
    await adapter.RemoveRange(prefix);

    // Assert
    var chunks = await adapter.LoadRange(prefix);
    chunks.Should().BeEmpty("all chunks should be removed");
  }


  [Fact]
  public async Task RemoveRange_NonExistentDirectory_DoesNotThrow()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var prefix = new StorageKey(["nonexistent"]);

    // Act & Assert
    var act = async () => await adapter.RemoveRange(prefix);
    await act.Should().NotThrowAsync("removing non-existent directory should be idempotent");
  }


  [Fact]
  public async Task GetChunkInfo_ReturnsMetadataWithoutLoadingData()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key1 = new StorageKey(["doc123", "snapshot", "hash1"]);
    var key2 = new StorageKey(["doc123", "incremental", "hash2"]);

    var data1 = new byte[100];
    var data2 = new byte[200];

    await adapter.Save(key1, data1);
    await adapter.Save(key2, data2);

    // Act
    var prefix = new StorageKey(["doc123"]);
    var infos = await adapter.GetChunkInfo(prefix);

    // Assert
    infos.Should().HaveCount(2);
    infos.Should().Contain(i => i.Key.Components[1] == "snapshot" && i.Key.Components[2] == "hash1" && i.Size == 100);
    infos.Should().Contain(i => i.Key.Components[1] == "incremental" && i.Key.Components[2] == "hash2" && i.Size == 200);
  }


  [Fact]
  public async Task Dispose_CleansUpResources()
  {
    // Arrange
    var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["test", "key"]);

    // Act
    adapter.Dispose();

    // Assert
    var act = async () => await adapter.Load(key);
    await act.Should().ThrowAsync<ObjectDisposedException>();
  }


  [Fact]
  public async Task Dispose_CanBeCalledMultipleTimes()
  {
    // Arrange
    var adapter = new FileSystemStorageAdapter(_testDirectory);

    // Act & Assert
    adapter.Dispose();
    var act = () => adapter.Dispose();
    act.Should().NotThrow("Dispose should be idempotent");
  }


  [Fact]
  public async Task Save_NullData_ThrowsArgumentNullException()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["test", "key"]);

    // Act & Assert
    var act = async () => await adapter.Save(key, null!);
    await act.Should().ThrowAsync<ArgumentNullException>();
  }


  [Fact]
  public async Task ConcurrentSaves_ToSameKey_AreSerializedCorrectly()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["test", "key"]);

    // Act - Start multiple concurrent saves
    var tasks = Enumerable.Range(0, 10).Select(i =>
    {
      var data = System.Text.Encoding.UTF8.GetBytes($"data-{i}");
      return adapter.Save(key, data);
    }).ToArray();

    await Task.WhenAll(tasks);

    // Assert - Should not throw, and last save wins
    var loaded = await adapter.Load(key);
    loaded.Should().NotBeNull("final save should be present");
  }


  [Fact]
  public async Task ConcurrentOperations_OnDifferentKeys_WorkInParallel()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);

    // Act - Save multiple different keys concurrently
    var tasks = Enumerable.Range(0, 100).Select(i =>
    {
      var key = new StorageKey([$"doc{i}", "snapshot", "hash"]);
      var data = System.Text.Encoding.UTF8.GetBytes($"data-{i}");
      return adapter.Save(key, data);
    }).ToArray();

    await Task.WhenAll(tasks);

    // Assert - All should be saved successfully
    for(int i = 0; i < 100; i++)
    {
      var key = new StorageKey([$"doc{i}", "snapshot", "hash"]);
      var loaded = await adapter.Load(key);
      loaded.Should().NotBeNull($"doc{i} should be saved");
    }
  }


  [Fact]
  public async Task CacheOptions_CustomSizeLimit_IsRespected()
  {
    // Arrange
    var options = new FileSystemStorageOptions
    {
      CacheSizeLimit = 1024, // 1 KB cache limit
      CacheSlidingExpiration = TimeSpan.FromMinutes(1)
    };

    using var adapter = new FileSystemStorageAdapter(_testDirectory, options);

    // Act - Save multiple large chunks to exceed cache size
    for(int i = 0; i < 10; i++)
    {
      var key = new StorageKey([$"doc{i}", "snapshot", "hash"]);
      var data = new byte[512]; // 512 bytes each
      await adapter.Save(key, data);
    }

    // Assert - Should not throw, cache eviction should occur
    // (We can't directly test cache eviction, but it should not cause errors)
    var testKey = new StorageKey(["doc5", "snapshot", "hash"]);
    var loaded = await adapter.Load(testKey);
    loaded.Should().NotBeNull("data should still be loadable from disk even if evicted from cache");
  }


  [Fact]
  [Trait("Category", "Bug")]
  public async Task LoadRange_ColdCache_DoesNotDeadlock()
  {
    // Arrange — save data, then create a fresh adapter (cold cache) over the same directory
    using var setupAdapter = new FileSystemStorageAdapter(_testDirectory);
    var docId = "doc123";
    await setupAdapter.Save(new StorageKey([docId, "snapshot", "hash1"]), "snap1"u8.ToArray());
    await setupAdapter.Save(new StorageKey([docId, "incremental", "hash2"]), "inc1"u8.ToArray());
    setupAdapter.Dispose();

    // Act — fresh adapter has an empty cache; LoadRange must read from disk without deadlocking
    using var coldAdapter = new FileSystemStorageAdapter(_testDirectory);
    var prefix = new StorageKey([docId]);

    // Use a timeout to detect deadlocks — the old code would hang forever here
    var loadTask = coldAdapter.LoadRange(prefix);
    var completed = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(5)));

    // Assert
    completed.Should().BeSameAs(loadTask, "LoadRange should complete without deadlocking");

    var chunks = await loadTask;
    chunks.Should().HaveCount(2, "both chunks should be loaded from disk on cold cache");
    chunks.Should().Contain(c => c.Key.Components[1] == "snapshot");
    chunks.Should().Contain(c => c.Key.Components[1] == "incremental");
  }


  [Fact]
  [Trait("Category", "Bug")]
  public async Task RemoveRange_ClearsCache_ImmediatelyAfterDeletion()
  {
    // Arrange — save data so it's both on disk and in cache
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var docId = "doc123";
    var key1 = new StorageKey([docId, "snapshot", "hash1"]);
    var key2 = new StorageKey([docId, "incremental", "hash2"]);
    var otherKey = new StorageKey(["other-doc", "snapshot", "hash3"]);

    await adapter.Save(key1, "snap1"u8.ToArray());
    await adapter.Save(key2, "inc1"u8.ToArray());
    await adapter.Save(otherKey, "other"u8.ToArray());

    // Warm the cache by loading
    (await adapter.Load(key1)).Should().NotBeNull();
    (await adapter.Load(key2)).Should().NotBeNull();
    (await adapter.Load(otherKey)).Should().NotBeNull();

    // Act — remove the doc123 range
    var prefix = new StorageKey([docId]);
    await adapter.RemoveRange(prefix);

    // Assert — cached data for doc123 must be gone immediately, not stale
    var loaded1 = await adapter.Load(key1);
    var loaded2 = await adapter.Load(key2);
    var loadedOther = await adapter.Load(otherKey);

    loaded1.Should().BeNull("cache entry should be evicted immediately by RemoveRange");
    loaded2.Should().BeNull("cache entry should be evicted immediately by RemoveRange");
    loadedOther.Should().NotBeNull("unrelated cache entries should not be affected");
  }


  [Fact]
  [Trait("Category", "Bug")]
  public async Task GetChunkInfo_EmptyPrefix_ReturnsAllStoredChunks()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);

    var key1 = new StorageKey(["doc111", "snapshot", "hash1"]);
    var key2 = new StorageKey(["doc222", "incremental", "hash2"]);
    var key3 = new StorageKey(["doc333", "snapshot", "hash3"]);

    await adapter.Save(key1, new byte[100]);
    await adapter.Save(key2, new byte[200]);
    await adapter.Save(key3, new byte[300]);

    // Act — empty prefix should return all stored chunks
    var emptyPrefix = new StorageKey([]);
    var infos = await adapter.GetChunkInfo(emptyPrefix);

    // Assert
    infos.Should().HaveCount(3, "empty prefix should match all stored chunks");
    infos.Should().Contain(i => i.Key.Components[0] == "doc111" && i.Size == 100);
    infos.Should().Contain(i => i.Key.Components[0] == "doc222" && i.Size == 200);
    infos.Should().Contain(i => i.Key.Components[0] == "doc333" && i.Size == 300);
  }


  [Fact]
  [Trait("Category", "Bug")]
  public async Task LoadRange_EmptyPrefix_ReturnsAllStoredData()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);

    var key1 = new StorageKey(["docAAA", "snapshot", "hash1"]);
    var key2 = new StorageKey(["docBBB", "incremental", "hash2"]);
    var key3 = new StorageKey(["docCCC", "snapshot", "hash3"]);

    var data1 = "snap1"u8.ToArray();
    var data2 = "inc1"u8.ToArray();
    var data3 = "snap3"u8.ToArray();

    await adapter.Save(key1, data1);
    await adapter.Save(key2, data2);
    await adapter.Save(key3, data3);

    // Act — empty prefix should return all stored data
    var emptyPrefix = new StorageKey([]);
    var chunks = await adapter.LoadRange(emptyPrefix);

    // Assert
    chunks.Should().HaveCount(3, "empty prefix should return all stored chunks");
    chunks.Should().Contain(c => c.Key.Components[0] == "docAAA" && c.Data.SequenceEqual(data1));
    chunks.Should().Contain(c => c.Key.Components[0] == "docBBB" && c.Data.SequenceEqual(data2));
    chunks.Should().Contain(c => c.Key.Components[0] == "docCCC" && c.Data.SequenceEqual(data3));
  }
}
