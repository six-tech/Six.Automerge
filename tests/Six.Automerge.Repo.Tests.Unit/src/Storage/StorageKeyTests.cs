using Six.Automerge.Repository.Storage;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Storage;

[Trait("Category", "Storage")]
public class StorageKeyTests
{
    [Fact]
    public void DocumentSnapshot_CreatesCorrectFormat()
    {
        // Arrange
        var documentId = "doc123";
        var hash = "abc123";

        // Act
        var key = StorageKey.DocumentSnapshot(documentId, hash);

        // Assert
        Assert.Equal(3, key.Components.Count);
        Assert.Equal(documentId, key.Components[0]);
        Assert.Equal("snapshot", key.Components[1]);
        Assert.Equal(hash, key.Components[2]);
    }

    [Fact]
    public void DocumentIncremental_CreatesCorrectFormat()
    {
        // Arrange
        var documentId = "doc456";
        var hash = "def456";

        // Act
        var key = StorageKey.DocumentIncremental(documentId, hash);

        // Assert
        Assert.Equal(3, key.Components.Count);
        Assert.Equal(documentId, key.Components[0]);
        Assert.Equal("incremental", key.Components[1]);
        Assert.Equal(hash, key.Components[2]);
    }

    [Fact]
    public void DocumentSyncState_CreatesCorrectFormat()
    {
        // Arrange
        var documentId = "doc789";
        var storageId = "peer1";

        // Act
        var key = StorageKey.DocumentSyncState(documentId, storageId);

        // Assert
        Assert.Equal(3, key.Components.Count);
        Assert.Equal(documentId, key.Components[0]);
        Assert.Equal("sync-state", key.Components[1]);
        Assert.Equal(storageId, key.Components[2]);
    }

    [Fact]
    public void Generic_CreatesCorrectFormat()
    {
        // Arrange
        var ns = "myNamespace";
        var key = "myKey";

        // Act
        var storageKey = StorageKey.Generic(ns, key);

        // Assert
        Assert.Equal(2, storageKey.Components.Count);
        Assert.Equal(ns, storageKey.Components[0]);
        Assert.Equal(key, storageKey.Components[1]);
    }

    [Fact]
    public void ToDebugString_JoinsComponentsWithSlash()
    {
        // Arrange
        var key = StorageKey.DocumentSnapshot("doc1", "hash1");

        // Act
        var debugString = key.ToDebugString();

        // Assert
        Assert.Equal("doc1/snapshot/hash1", debugString);
    }

    [Fact]
    public void ToDebugString_EmptyComponents_ReturnsEmpty()
    {
        // Arrange
        var key = new StorageKey(Array.Empty<string>());

        // Act
        var debugString = key.ToDebugString();

        // Assert
        Assert.Equal(string.Empty, debugString);
    }

    [Fact]
    public void ToString_CallsToDebugString()
    {
        // Arrange
        var key = StorageKey.Generic("ns", "key");

        // Act
        var result = key.ToString();

        // Assert
        Assert.Equal("ns/key", result);
    }

    [Fact]
    public void StartsWith_MatchingPrefix_ReturnsTrue()
    {
        // Arrange
        var fullKey = StorageKey.DocumentSnapshot("doc1", "hash1");
        var prefix = new StorageKey(new[] { "doc1" });

        // Act
        var result = fullKey.StartsWith(prefix);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void StartsWith_NonMatchingPrefix_ReturnsFalse()
    {
        // Arrange
        var fullKey = StorageKey.DocumentSnapshot("doc1", "hash1");
        var prefix = new StorageKey(new[] { "doc2" });

        // Act
        var result = fullKey.StartsWith(prefix);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void StartsWith_EmptyPrefix_ReturnsTrue()
    {
        // Arrange
        var fullKey = StorageKey.DocumentSnapshot("doc1", "hash1");
        var prefix = new StorageKey(Array.Empty<string>());

        // Act
        var result = fullKey.StartsWith(prefix);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void StartsWith_PrefixLongerThanKey_ReturnsFalse()
    {
        // Arrange
        var shortKey = new StorageKey(new[] { "doc1" });
        var longPrefix = new StorageKey(new[] { "doc1", "snapshot", "hash1" });

        // Act
        var result = shortKey.StartsWith(longPrefix);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void StartsWith_ExactMatch_ReturnsTrue()
    {
        // Arrange
        var key = StorageKey.Generic("ns", "key");
        var prefix = StorageKey.Generic("ns", "key");

        // Act
        var result = key.StartsWith(prefix);

        // Assert
        Assert.True(result);
    }
}
