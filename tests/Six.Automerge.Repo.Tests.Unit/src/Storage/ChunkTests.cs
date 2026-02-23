using Six.Automerge.Repository.Storage;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Storage;

[Trait("Category", "Storage")]
public class ChunkTests
{
    [Fact]
    public void Chunk_Type_ExtractsSnapshotFromKey()
    {
        // Arrange
        var key = StorageKey.DocumentSnapshot("doc1", "hash1");
        var data = new byte[] { 1, 2, 3 };

        // Act
        var chunk = new Chunk(key, data);

        // Assert
        Assert.Equal(ChunkType.Snapshot, chunk.Type);
    }

    [Fact]
    public void Chunk_Type_ExtractsIncrementalFromKey()
    {
        // Arrange
        var key = StorageKey.DocumentIncremental("doc1", "hash1");
        var data = new byte[] { 1, 2, 3 };

        // Act
        var chunk = new Chunk(key, data);

        // Assert
        Assert.Equal(ChunkType.Incremental, chunk.Type);
    }

    [Fact]
    public void Chunk_Type_ExtractsSyncStateFromKey()
    {
        // Arrange
        var key = StorageKey.DocumentSyncState("doc1", "peer1");
        var data = new byte[] { 1, 2, 3 };

        // Act
        var chunk = new Chunk(key, data);

        // Assert
        Assert.Equal(ChunkType.SyncState, chunk.Type);
    }

    [Fact]
    public void ChunkInfo_Type_ExtractsCorrectType()
    {
        // Arrange
        var key = StorageKey.DocumentSnapshot("doc1", "hash1");

        // Act
        var info = new ChunkInfo(key, 100);

        // Assert
        Assert.Equal(ChunkType.Snapshot, info.Type);
        Assert.Equal(100, info.Size);
    }

    [Fact]
    public void ChunkTypeFromKey_InvalidKey_ThrowsArgumentException()
    {
        // Arrange
        var invalidKey = new StorageKey(new[] { "doc1" }); // Only one component

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Chunk.ChunkTypeFromKey(invalidKey));
        Assert.Contains("Invalid storage key format", ex.Message);
    }

    [Fact]
    public void ChunkTypeFromKey_UnknownType_ThrowsArgumentException()
    {
        // Arrange
        var invalidKey = new StorageKey(new[] { "doc1", "unknown-type", "hash1" });

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => Chunk.ChunkTypeFromKey(invalidKey));
        Assert.Contains("Unknown chunk type", ex.Message);
    }

    [Fact]
    public void Chunk_PreservesKeyAndData()
    {
        // Arrange
        var key = StorageKey.DocumentSnapshot("doc1", "hash1");
        var data = new byte[] { 10, 20, 30, 40 };

        // Act
        var chunk = new Chunk(key, data);

        // Assert
        Assert.Equal(key, chunk.Key);
        Assert.Equal(data, chunk.Data);
        Assert.Equal(4, chunk.Data.Length);
    }

    [Fact]
    public void ChunkInfo_PreservesKeyAndSize()
    {
        // Arrange
        var key = StorageKey.DocumentIncremental("doc2", "hash2");
        var size = 12345;

        // Act
        var info = new ChunkInfo(key, size);

        // Assert
        Assert.Equal(key, info.Key);
        Assert.Equal(size, info.Size);
    }
}
