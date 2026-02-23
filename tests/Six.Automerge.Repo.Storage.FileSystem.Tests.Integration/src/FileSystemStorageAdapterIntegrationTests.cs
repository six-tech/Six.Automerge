using FluentAssertions;
using Six.Automerge.Repository.Storage;
using Six.Automerge.Repository.Storage.FileSystem;
using Xunit;

namespace Six.Automerge.Repo.Storage.FileSystem.Tests;

/// <summary>
/// Integration tests for FileSystemStorageAdapter.
/// Tests more complex scenarios and real-world usage patterns.
/// </summary>
public sealed class FileSystemStorageAdapterIntegrationTests : IDisposable
{
  readonly string _testDirectory;

  public FileSystemStorageAdapterIntegrationTests()
  {
    _testDirectory = Path.Combine(Path.GetTempPath(), "automerge-fs-integration-tests", Guid.NewGuid().ToString());
  }

  public void Dispose()
  {
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
  public async Task FullDocumentCycle_SaveLoadDelete()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var docId = "doc-" + Guid.NewGuid();

    // Simulate saving a document with snapshot + incrementals
    var snapshot = new byte[1024]; // 1 KB snapshot
    var inc1 = new byte[256];
    var inc2 = new byte[512];

    // Fill with test data
    Random.Shared.NextBytes(snapshot);
    Random.Shared.NextBytes(inc1);
    Random.Shared.NextBytes(inc2);

    // Act - Save
    await adapter.Save(new StorageKey([docId, "snapshot", "hash1"]), snapshot);
    await adapter.Save(new StorageKey([docId, "incremental", "hash2"]), inc1);
    await adapter.Save(new StorageKey([docId, "incremental", "hash3"]), inc2);

    // Act - Load all chunks
    var chunks = await adapter.LoadRange(new StorageKey([docId]));

    // Assert - All chunks loaded
    chunks.Should().HaveCount(3);
    chunks.Should().Contain(c => c.Data.Length == 1024);
    chunks.Should().Contain(c => c.Data.Length == 256);
    chunks.Should().Contain(c => c.Data.Length == 512);

    // Act - Delete all
    await adapter.RemoveRange(new StorageKey([docId]));

    // Assert - Nothing remains
    var remaining = await adapter.LoadRange(new StorageKey([docId]));
    remaining.Should().BeEmpty();
  }


  [Fact]
  public async Task CrossProcessPersistence_DataSurvivesAdapterRecreation()
  {
    // Arrange
    var key = new StorageKey(["persistent-doc", "snapshot", "hash"]);
    var originalData = "This data should persist"u8.ToArray();

    // Act - Save with first adapter instance
    using(var adapter1 = new FileSystemStorageAdapter(_testDirectory))
    {
      await adapter1.Save(key, originalData);
    }

    // Act - Load with second adapter instance
    using var adapter2 = new FileSystemStorageAdapter(_testDirectory);
    var loaded = await adapter2.Load(key);

    // Assert
    loaded.Should().NotBeNull();
    loaded.Should().BeEquivalentTo(originalData, "data should persist across adapter instances");
  }


  [Fact]
  public async Task LargeFileHandling_SaveAndLoadLargeData()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["large-doc", "snapshot", "hash"]);

    // Create 10 MB of test data
    var largeData = new byte[10 * 1024 * 1024];
    Random.Shared.NextBytes(largeData);

    // Act
    await adapter.Save(key, largeData);
    var loaded = await adapter.Load(key);

    // Assert
    loaded.Should().NotBeNull();
    loaded.Should().HaveCount(largeData.Length);
    loaded.Should().BeEquivalentTo(largeData, "large data should be saved and loaded correctly");
  }


  [Fact]
  public async Task DeepDirectoryNesting_HandlesComplexKeys()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["doc123", "level1", "level2", "level3", "level4", "final"]);
    var data = "nested data"u8.ToArray();

    // Act
    await adapter.Save(key, data);
    var loaded = await adapter.Load(key);

    // Assert
    loaded.Should().NotBeNull();
    loaded.Should().BeEquivalentTo(data);
  }


  [Fact]
  public async Task CachePerformance_SecondLoadFasterThanFirst()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    var key = new StorageKey(["perf-doc", "snapshot", "hash"]);
    var data = new byte[1024 * 1024]; // 1 MB
    Random.Shared.NextBytes(data);

    await adapter.Save(key, data);

    // Act - First load (from disk)
    var sw1 = System.Diagnostics.Stopwatch.StartNew();
    var loaded1 = await adapter.Load(key);
    sw1.Stop();

    // Act - Second load (from cache)
    var sw2 = System.Diagnostics.Stopwatch.StartNew();
    var loaded2 = await adapter.Load(key);
    sw2.Stop();

    // Assert
    loaded1.Should().BeEquivalentTo(data);
    loaded2.Should().BeEquivalentTo(data);

    // Note: Cache should be faster, but we won't make hard assertions about timing
    // as it depends on system performance. Just verify both loads succeed.
    sw2.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(sw1.ElapsedMilliseconds,
      "cached load should be at least as fast as disk load");
  }


  [Fact]
  public async Task MultipleDocuments_IndependentStorage()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);

    // Create multiple documents
    var docs = Enumerable.Range(0, 50).Select(i => new
    {
      Id = $"doc-{i}",
      Snapshot = System.Text.Encoding.UTF8.GetBytes($"snapshot-{i}"),
      Incremental = System.Text.Encoding.UTF8.GetBytes($"incremental-{i}")
    }).ToList();

    // Act - Save all documents
    foreach(var doc in docs)
    {
      await adapter.Save(new StorageKey([doc.Id, "snapshot", "hash"]), doc.Snapshot);
      await adapter.Save(new StorageKey([doc.Id, "incremental", "hash"]), doc.Incremental);
    }

    // Act - Verify each document independently
    foreach(var doc in docs)
    {
      var snapshot = await adapter.Load(new StorageKey([doc.Id, "snapshot", "hash"]));
      var incremental = await adapter.Load(new StorageKey([doc.Id, "incremental", "hash"]));

      // Assert
      snapshot.Should().BeEquivalentTo(doc.Snapshot, $"snapshot for {doc.Id} should be correct");
      incremental.Should().BeEquivalentTo(doc.Incremental, $"incremental for {doc.Id} should be correct");
    }
  }


  [Fact]
  public async Task CacheEviction_StillLoadsFromDisk()
  {
    // Arrange - Small cache that will be exceeded
    var options = new FileSystemStorageOptions
    {
      CacheSizeLimit = 1024, // 1 KB
      CacheSlidingExpiration = TimeSpan.FromSeconds(1)
    };

    using var adapter = new FileSystemStorageAdapter(_testDirectory, options);

    // Save multiple files that exceed cache size
    var keys = new List<StorageKey>();
    for(int i = 0; i < 10; i++)
    {
      var key = new StorageKey([$"doc{i}", "snapshot", "hash"]);
      keys.Add(key);
      var data = new byte[512]; // 512 bytes each = 5 KB total
      Random.Shared.NextBytes(data);
      await adapter.Save(key, data);
    }

    // Wait for potential cache eviction
    await Task.Delay(1500);

    // Act - Load all keys again (some will be evicted from cache)
    foreach(var key in keys)
    {
      var loaded = await adapter.Load(key);

      // Assert - Should still load from disk even if cache evicted
      loaded.Should().NotBeNull($"key {key.ToDebugString()} should load from disk");
      loaded.Should().HaveCount(512);
    }
  }


  [Fact]
  public async Task DirectorySharding_DistributesFilesAcrossDirectories()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);

    // Create documents with different first 2 chars to test sharding
    var docs = new[]
    {
      "aa1111",
      "ab2222",
      "ac3333",
      "ba4444",
      "bb5555"
    };

    // Act
    foreach(var docId in docs)
    {
      await adapter.Save(new StorageKey([docId, "snapshot", "hash"]), "data"u8.ToArray());
    }

    // Assert - Verify sharding created expected directory structure
    Directory.Exists(Path.Combine(_testDirectory, "aa")).Should().BeTrue();
    Directory.Exists(Path.Combine(_testDirectory, "ab")).Should().BeTrue();
    Directory.Exists(Path.Combine(_testDirectory, "ac")).Should().BeTrue();
    Directory.Exists(Path.Combine(_testDirectory, "ba")).Should().BeTrue();
    Directory.Exists(Path.Combine(_testDirectory, "bb")).Should().BeTrue();

    // Verify files are in correct shard directories
    File.Exists(Path.Combine(_testDirectory, "aa", "1111", "snapshot", "hash")).Should().BeTrue();
    File.Exists(Path.Combine(_testDirectory, "ab", "2222", "snapshot", "hash")).Should().BeTrue();
    File.Exists(Path.Combine(_testDirectory, "ac", "3333", "snapshot", "hash")).Should().BeTrue();
  }


  [Fact]
  public async Task StressTest_ManySmallOperations()
  {
    // Arrange
    using var adapter = new FileSystemStorageAdapter(_testDirectory);
    const int operationCount = 1000;

    // Act - Perform many small save operations
    var tasks = Enumerable.Range(0, operationCount).Select(async i =>
    {
      var key = new StorageKey([$"stress-{i % 10}", "chunk", $"hash-{i}"]);
      var data = System.Text.Encoding.UTF8.GetBytes($"data-{i}");
      await adapter.Save(key, data);
    });

    await Task.WhenAll(tasks);

    // Assert - Verify a sample of saves
    for(int i = 0; i < 100; i += 10)
    {
      var key = new StorageKey([$"stress-{i % 10}", "chunk", $"hash-{i}"]);
      var loaded = await adapter.Load(key);
      loaded.Should().NotBeNull($"operation {i} should have saved successfully");
    }
  }
}
