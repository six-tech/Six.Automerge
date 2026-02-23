using Six.Automerge.Repository.Storage;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Storage;

[Trait("Category", "Storage")]
public class InMemoryStorageAdapterTests
{
    [Fact]
    public async Task Save_ThenLoad_ReturnsSameData()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "key1");
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await adapter.Save(key, data);
        var loaded = await adapter.Load(key);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(data, loaded);
    }

    [Fact]
    public async Task Load_NonExistentKey_ReturnsNull()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "nonexistent");

        // Act
        var result = await adapter.Load(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_OverwritesExistingData()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "key1");
        var originalData = new byte[] { 1, 2, 3 };
        var newData = new byte[] { 4, 5, 6, 7 };

        // Act
        await adapter.Save(key, originalData);
        await adapter.Save(key, newData);
        var loaded = await adapter.Load(key);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(newData, loaded);
    }

    [Fact]
    public async Task Remove_DeletesData()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "key1");
        var data = new byte[] { 1, 2, 3 };

        // Act
        await adapter.Save(key, data);
        await adapter.Remove(key);
        var loaded = await adapter.Load(key);

        // Assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Remove_NonExistentKey_DoesNotThrow()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "nonexistent");

        // Act & Assert - should not throw
        await adapter.Remove(key);
    }

    [Fact]
    public async Task LoadRange_ReturnsMatchingKeys()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var doc1Snapshot = StorageKey.DocumentSnapshot("doc1", "hash1");
        var doc1Incremental = StorageKey.DocumentIncremental("doc1", "hash2");
        var doc2Snapshot = StorageKey.DocumentSnapshot("doc2", "hash3");

        await adapter.Save(doc1Snapshot, new byte[] { 1 });
        await adapter.Save(doc1Incremental, new byte[] { 2 });
        await adapter.Save(doc2Snapshot, new byte[] { 3 });

        // Act
        var prefix = new StorageKey(new[] { "doc1" });
        var results = await adapter.LoadRange(prefix);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, c => c.Key.ToDebugString() == doc1Snapshot.ToDebugString());
        Assert.Contains(results, c => c.Key.ToDebugString() == doc1Incremental.ToDebugString());
    }

    [Fact]
    public async Task LoadRange_EmptyResult_ReturnsEmptyList()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var prefix = new StorageKey(new[] { "nonexistent" });

        // Act
        var results = await adapter.LoadRange(prefix);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task RemoveRange_DeletesMatchingKeys()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var doc1Snapshot = StorageKey.DocumentSnapshot("doc1", "hash1");
        var doc1Incremental = StorageKey.DocumentIncremental("doc1", "hash2");
        var doc2Snapshot = StorageKey.DocumentSnapshot("doc2", "hash3");

        await adapter.Save(doc1Snapshot, new byte[] { 1 });
        await adapter.Save(doc1Incremental, new byte[] { 2 });
        await adapter.Save(doc2Snapshot, new byte[] { 3 });

        // Act
        var prefix = new StorageKey(new[] { "doc1" });
        await adapter.RemoveRange(prefix);

        // Assert
        Assert.Null(await adapter.Load(doc1Snapshot));
        Assert.Null(await adapter.Load(doc1Incremental));
        Assert.NotNull(await adapter.Load(doc2Snapshot)); // doc2 should remain
    }

    [Fact]
    public async Task RemoveRange_NoMatches_DoesNotThrow()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var prefix = new StorageKey(new[] { "nonexistent" });

        // Act & Assert - should not throw
        await adapter.RemoveRange(prefix);
    }

    [Fact]
    public async Task GetChunkInfo_ReturnsCorrectMetadata()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key1 = StorageKey.DocumentSnapshot("doc1", "hash1");
        var key2 = StorageKey.DocumentIncremental("doc1", "hash2");
        var data1 = new byte[100];
        var data2 = new byte[50];

        await adapter.Save(key1, data1);
        await adapter.Save(key2, data2);

        // Act
        var prefix = new StorageKey(new[] { "doc1" });
        var results = await adapter.GetChunkInfo(prefix);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, info => info.Key.ToDebugString() == key1.ToDebugString() && info.Size == 100);
        Assert.Contains(results, info => info.Key.ToDebugString() == key2.ToDebugString() && info.Size == 50);
    }

    [Fact]
    public async Task GetChunkInfo_EmptyResult_ReturnsEmptyList()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var prefix = new StorageKey(new[] { "nonexistent" });

        // Act
        var results = await adapter.GetChunkInfo(prefix);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task Count_ReturnsCorrectNumberOfKeys()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();

        // Act & Assert
        Assert.Equal(0, adapter.Count);

        await adapter.Save(StorageKey.Generic("test", "key1"), new byte[] { 1 });
        Assert.Equal(1, adapter.Count);

        await adapter.Save(StorageKey.Generic("test", "key2"), new byte[] { 2 });
        Assert.Equal(2, adapter.Count);

        await adapter.Remove(StorageKey.Generic("test", "key1"));
        Assert.Equal(1, adapter.Count);
    }

    [Fact]
    public async Task Clear_RemovesAllData()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        await adapter.Save(StorageKey.Generic("test", "key1"), new byte[] { 1 });
        await adapter.Save(StorageKey.Generic("test", "key2"), new byte[] { 2 });
        await adapter.Save(StorageKey.Generic("test", "key3"), new byte[] { 3 });

        // Act
        await adapter.Clear();

        // Assert
        Assert.Equal(0, adapter.Count);
        Assert.Null(await adapter.Load(StorageKey.Generic("test", "key1")));
        Assert.Null(await adapter.Load(StorageKey.Generic("test", "key2")));
        Assert.Null(await adapter.Load(StorageKey.Generic("test", "key3")));
    }

    [Fact]
    public async Task Save_CopiesData_MutationDoesNotAffectStored()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "key1");
        var data = new byte[] { 1, 2, 3 };

        // Act
        await adapter.Save(key, data);
        data[0] = 99; // Mutate original array
        var loaded = await adapter.Load(key);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded[0]); // Should still be 1, not 99
    }

    [Fact]
    public async Task Load_CopiesData_MutationDoesNotAffectStored()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        var key = StorageKey.Generic("test", "key1");
        var data = new byte[] { 1, 2, 3 };

        // Act
        await adapter.Save(key, data);
        var loaded1 = await adapter.Load(key);
        loaded1![0] = 99; // Mutate loaded array
        var loaded2 = await adapter.Load(key);

        // Assert
        Assert.NotNull(loaded2);
        Assert.Equal(1, loaded2[0]); // Should still be 1, not 99
    }

    [Fact]
    public async Task ConcurrentOperations_AreThreadSafe()
    {
        // Arrange
        using var adapter = new InMemoryStorageAdapter();
        const int operationCount = 100;

        // Act - perform many concurrent saves
        var tasks = Enumerable.Range(0, operationCount)
            .Select(i => adapter.Save(
                StorageKey.Generic("test", $"key{i}"),
                new byte[] { (byte)i }))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - all data should be present
        Assert.Equal(operationCount, adapter.Count);

        for (int i = 0; i < operationCount; i++)
        {
            var data = await adapter.Load(StorageKey.Generic("test", $"key{i}"));
            Assert.NotNull(data);
            Assert.Equal((byte)i, data[0]);
        }
    }

    [Fact]
    public async Task Dispose_ClearsData()
    {
        // Arrange
        var adapter = new InMemoryStorageAdapter();
        await adapter.Save(StorageKey.Generic("test", "key1"), new byte[] { 1 });

        // Act
        adapter.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => adapter.Count);
    }

    [Fact]
    public async Task AfterDispose_OperationsThrow()
    {
        // Arrange
        var adapter = new InMemoryStorageAdapter();
        adapter.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await adapter.Load(StorageKey.Generic("test", "key")));

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await adapter.Save(StorageKey.Generic("test", "key"), new byte[] { 1 }));

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await adapter.Remove(StorageKey.Generic("test", "key")));
    }
}
