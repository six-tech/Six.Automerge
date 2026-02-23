using System;
using System.Linq;
using Xunit;
using Six.Automerge;

namespace Six.Automerge.FFI.Test.Unit;

[Trait("Category", "Metadata")]
public class AutomergeChangeMetadataTests
{
  [Fact]
  public void GetChangeMetadata_WithValidHash_ShouldReturnMetadata()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    doc.PutString(doc.RootId, "title", "Hello World");
    var hash = doc.Commit("Initial commit", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    Assert.NotNull(hash);

    // Act
    var metadata = doc.GetChangeMetadata(hash);

    // Assert
    Assert.NotNull(metadata);
    Assert.Equal(hash, metadata.Hash);
    Assert.NotNull(metadata.Actor);
    Assert.NotEmpty(metadata.Actor);
    Assert.Equal(1UL, metadata.Sequence);
    Assert.True(metadata.TimestampMillis > 0);
    Assert.Equal("Initial commit", metadata.Message);
    Assert.NotNull(metadata.Dependencies);
  }

  [Fact]
  public void GetChangeMetadata_WithMultipleCommits_ShouldReturnCorrectSequence()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();

    doc.PutString(doc.RootId, "key1", "value1");
    var hash1 = doc.Commit("First commit");

    doc.PutString(doc.RootId, "key2", "value2");
    var hash2 = doc.Commit("Second commit");

    doc.PutString(doc.RootId, "key3", "value3");
    var hash3 = doc.Commit("Third commit");

    // Act
    var metadata1 = doc.GetChangeMetadata(hash1!);
    var metadata2 = doc.GetChangeMetadata(hash2!);
    var metadata3 = doc.GetChangeMetadata(hash3!);

    // Assert
    Assert.NotNull(metadata1);
    Assert.NotNull(metadata2);
    Assert.NotNull(metadata3);

    Assert.Equal(1UL, metadata1.Sequence);
    Assert.Equal(2UL, metadata2.Sequence);
    Assert.Equal(3UL, metadata3.Sequence);

    Assert.Equal("First commit", metadata1.Message);
    Assert.Equal("Second commit", metadata2.Message);
    Assert.Equal("Third commit", metadata3.Message);

    // All commits should have the same actor
    Assert.Equal(metadata1.Actor, metadata2.Actor);
    Assert.Equal(metadata2.Actor, metadata3.Actor);
  }

  [Fact]
  public void GetChangeMetadata_WithInvalidHash_ShouldReturnNull()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    doc.PutString(doc.RootId, "key", "value");
    doc.Commit();

    var invalidHash = "0000000000000000000000000000000000000000000000000000000000000000";

    // Act
    var metadata = doc.GetChangeMetadata(invalidHash);

    // Assert
    Assert.Null(metadata);
  }

  [Fact]
  public void GetChangeMetadata_WithNullHash_ShouldThrow()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => doc.GetChangeMetadata(null!));
  }

  [Fact]
  public void GetChangeMetadata_WithEmptyHash_ShouldThrow()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();

    // Act & Assert
    Assert.Throws<ArgumentException>(() => doc.GetChangeMetadata(""));
  }

  [Fact]
  public void GetChangeMetadata_WithNoMessage_ShouldHaveNullMessage()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    doc.PutString(doc.RootId, "key", "value");
    var hash = doc.Commit(); // No message provided

    // Act
    var metadata = doc.GetChangeMetadata(hash!);

    // Assert
    Assert.NotNull(metadata);
    Assert.Null(metadata.Message);
  }

  [Fact]
  public void GetChangeMetadata_WithDependencies_ShouldReturnDeps()
  {
    // Arrange
    using var doc1 = AutomergeDocument.Create();
    doc1.PutString(doc1.RootId, "key1", "value1");
    var hash1 = doc1.Commit("First change");

    using var doc2 = AutomergeDocument.Create();
    doc2.PutString(doc2.RootId, "key2", "value2");
    var hash2 = doc2.Commit("Second change");

    // Merge creates a change that depends on both previous changes
    doc1.Merge(doc2);
    var heads = doc1.GetHeads();

    // Act
    var metadata1 = doc1.GetChangeMetadata(hash1!);
    var metadata2 = doc1.GetChangeMetadata(hash2!);

    // Assert
    Assert.NotNull(metadata1);
    Assert.NotNull(metadata2);

    // First changes should have empty dependencies (they're root changes)
    Assert.Empty(metadata1.Dependencies);
    Assert.Empty(metadata2.Dependencies);
  }

  [Fact]
  public void GetChangeMetadata_ToString_ShouldFormatNicely()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    doc.PutString(doc.RootId, "key", "value");
    var hash = doc.Commit("Test commit");

    // Act
    var metadata = doc.GetChangeMetadata(hash!);
    var str = metadata!.ToString();

    // Assert
    Assert.Contains("Change", str);
    Assert.Contains(hash!.Substring(0, 8), str);
    Assert.Contains(metadata.Actor, str);
    Assert.Contains("seq=1", str);
    Assert.Contains("Test commit", str);
  }

  [Fact]
  public void DecodedChange_Constructor_ShouldInitializeAllFields()
  {
    // Arrange & Act
    var change = new DecodedChange(
      hash: "abcd1234",
      actor: "actor123",
      sequence: 42,
      timestampMillis: 1234567890,
      message: "Test message",
      dependencies: new[] { "dep1", "dep2" });

    // Assert
    Assert.Equal("abcd1234", change.Hash);
    Assert.Equal("actor123", change.Actor);
    Assert.Equal(42UL, change.Sequence);
    Assert.Equal(1234567890, change.TimestampMillis);
    Assert.Equal("Test message", change.Message);
    Assert.Equal(2, change.Dependencies.Count);
    Assert.Equal("dep1", change.Dependencies[0]);
    Assert.Equal("dep2", change.Dependencies[1]);
  }
}
