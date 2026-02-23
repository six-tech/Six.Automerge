using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.FFI.Test.Unit;

// Helper to create valid test document IDs
internal static class TestDocumentIdsMetadata
{
  public static DocumentId CreateTestDocId() => DocumentId.Generate();
}

[Trait("Category", "DocHandle")]
public class DocHandleMetadataTests
{
  [Fact]
  public void Metadata_WithNoParameter_ShouldReturnLatestHeadMetadata()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId(), document: doc);

    handle.Change(d =>
    {
      d.PutString(d.RootId, "title", "Test Document");
    }, "Initial commit");

    // Act
    var metadata = handle.Metadata();

    // Assert
    Assert.NotNull(metadata);
    Assert.NotNull(metadata.Hash);
    Assert.NotNull(metadata.Actor);
    Assert.Equal(1UL, metadata.Sequence);
    Assert.Equal("Initial commit", metadata.Message);
  }

  [Fact]
  public void Metadata_WithSpecificHash_ShouldReturnThatChangeMetadata()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId(), document: doc);

    var change1 = handle.Change(d =>
    {
      d.PutString(d.RootId, "key1", "value1");
    }, "First commit");

    var change2 = handle.Change(d =>
    {
      d.PutString(d.RootId, "key2", "value2");
    }, "Second commit");

    // Act
    var metadata1 = handle.Metadata(change1!.ChangeHash);
    var metadata2 = handle.Metadata(change2!.ChangeHash);

    // Assert
    Assert.NotNull(metadata1);
    Assert.NotNull(metadata2);
    Assert.Equal(change1.ChangeHash, metadata1.Hash);
    Assert.Equal(change2.ChangeHash, metadata2.Hash);
    Assert.Equal("First commit", metadata1.Message);
    Assert.Equal("Second commit", metadata2.Message);
    Assert.Equal(1UL, metadata1.Sequence);
    Assert.Equal(2UL, metadata2.Sequence);
  }

  [Fact]
  public void Metadata_WithInvalidHash_ShouldReturnNull()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId(), document: doc);

    handle.Change(d =>
    {
      d.PutString(d.RootId, "key", "value");
    });

    var invalidHash = "0000000000000000000000000000000000000000000000000000000000000000";

    // Act
    var metadata = handle.Metadata(invalidHash);

    // Assert
    Assert.Null(metadata);
  }

  [Fact]
  public void Metadata_OnDocumentWithNoChanges_ShouldReturnNull()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId(), document: doc);

    // Act
    var metadata = handle.Metadata();

    // Assert
    Assert.Null(metadata);
  }

  [Fact]
  public void Metadata_BeforeReady_ShouldThrow()
  {
    // Arrange
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId());

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => handle.Metadata());
  }

  [Fact]
  public void Metadata_AfterMultipleChanges_ShouldTrackSequenceCorrectly()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "v", "1"));
    handle.Change(d => d.PutString(d.RootId, "v", "2"));
    handle.Change(d => d.PutString(d.RootId, "v", "3"));

    // Act
    var metadata = handle.Metadata();

    // Assert
    Assert.NotNull(metadata);
    Assert.Equal(3UL, metadata.Sequence);
  }

  [Fact]
  public void Metadata_ActorShouldBeConsistent()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIdsMetadata.CreateTestDocId(), document: doc);

    var change1 = handle.Change(d => d.PutString(d.RootId, "a", "1"));
    var change2 = handle.Change(d => d.PutString(d.RootId, "b", "2"));

    // Act
    var metadata1 = handle.Metadata(change1!.ChangeHash);
    var metadata2 = handle.Metadata(change2!.ChangeHash);

    // Assert
    Assert.NotNull(metadata1);
    Assert.NotNull(metadata2);
    Assert.Equal(metadata1.Actor, metadata2.Actor);
  }
}
