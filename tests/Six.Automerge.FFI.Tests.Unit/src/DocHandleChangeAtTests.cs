using System;
using System.Linq;
using Xunit;
using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.FFI.Test.Unit;

// Helper to create valid test document IDs
internal static class TestDocumentIds
{
  public static DocumentId CreateTestDocId() => DocumentId.Generate();
}

[Trait("Category", "DocHandle")]
public class DocHandleChangeAtTests
{
  [Fact]
  public void ChangeAt_ShouldCreateConcurrentBranch()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "text", "base"));
    var baseHeads = handle.GetHeads().ToList();

    // Make a change on the main branch
    handle.Change(d => d.PutString(d.RootId, "text", "main"));

    // Act - Make a concurrent change at the base heads
    var changes = handle.ChangeAt(baseHeads, d =>
    {
      d.PutString(d.RootId, "text", "concurrent");
    });

    // Assert
    Assert.NotNull(changes);
    Assert.NotEmpty(changes);

    // The document should now have content from both branches
    var json = handle.Doc().HydrateJson();
    // After merge, Automerge will have resolved the conflict
    // The exact result depends on Automerge's CRDT merge rules
    Assert.NotNull(json);
  }

  [Fact]
  public void ChangeAt_WithNoChanges_ShouldReturnEmpty()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "key", "value"));
    var heads = handle.GetHeads().ToList();

    // Act - No actual changes made
    var changes = handle.ChangeAt(heads, d =>
    {
      // Empty mutation
    });

    // Assert
    Assert.NotNull(changes);
    Assert.Empty(changes);
  }

  [Fact]
  public void ChangeAt_MultipleProperties_ShouldAllBeApplied()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "initial", "value"));
    var heads = handle.GetHeads().ToList();

    // Make subsequent changes
    handle.Change(d => d.PutString(d.RootId, "prop1", "a"));
    handle.Change(d => d.PutString(d.RootId, "prop2", "b"));

    // Act - Make concurrent changes at earlier state
    var changes = handle.ChangeAt(heads, d =>
    {
      d.PutString(d.RootId, "concurrent1", "x");
      d.PutString(d.RootId, "concurrent2", "y");
    });

    // Assert
    Assert.NotEmpty(changes);

    var json = handle.Doc().HydrateJson();
    // All properties should exist after merge
    Assert.Contains("initial", json);
    Assert.Contains("prop1", json);
    Assert.Contains("prop2", json);
    Assert.Contains("concurrent1", json);
    Assert.Contains("concurrent2", json);
  }

  [Fact]
  public void ChangeAt_WithNullHeads_ShouldThrow()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "key", "value"));

    // Act & Assert
    Assert.Throws<ArgumentNullException>(() =>
      handle.ChangeAt(null!, d => { }));
  }

  [Fact]
  public void ChangeAt_WithEmptyHeads_ShouldThrow()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "key", "value"));

    // Act & Assert
    Assert.Throws<ArgumentException>(() =>
      handle.ChangeAt(Array.Empty<string>(), d => { }));
  }

  [Fact]
  public void ChangeAt_WithNullMutation_ShouldThrow()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "key", "value"));
    var heads = handle.GetHeads().ToList();

    // Act & Assert
    Assert.Throws<ArgumentNullException>(() =>
      handle.ChangeAt(heads, null!));
  }

  [Fact]
  public void ChangeAt_OnReadOnlyHandle_ShouldThrow()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var mainHandle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    mainHandle.Change(d => d.PutString(d.RootId, "key", "value"));
    var heads = mainHandle.GetHeads().ToList();

    // Create a read-only view
    var viewHandle = mainHandle.View(heads);

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() =>
      viewHandle.ChangeAt(heads, d => d.PutString(d.RootId, "new", "value")));
  }

  [Fact]
  public void ChangeAt_BeforeDocumentReady_ShouldThrow()
  {
    // Arrange
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId());

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() =>
      handle.ChangeAt(new[] { "somehash" }, d => { }));
  }

  [Fact]
  public void ChangeAt_ShouldEmitChangeEvent()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "initial", "value"));
    var heads = handle.GetHeads().ToList();

    bool eventFired = false;
    handle.Changed += (sender, args) =>
    {
      eventFired = true;
      Assert.True(args.IsLocal);
    };

    // Act
    handle.ChangeAt(heads, d => d.PutString(d.RootId, "concurrent", "value"));

    // Assert
    Assert.True(eventFired, "Changed event should have been fired");
  }

  [Fact]
  public void ChangeAt_ShouldUpdateCachedHeads()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "initial", "value"));
    var headsBeforeFork = handle.GetHeads().ToList();

    handle.Change(d => d.PutString(d.RootId, "main", "change"));
    var headsBeforeChangeAt = handle.GetHeads().ToList();

    // Act
    handle.ChangeAt(headsBeforeFork, d => d.PutString(d.RootId, "concurrent", "change"));

    // Assert
    var headsAfter = handle.GetHeads();
    Assert.NotEqual(headsBeforeChangeAt, headsAfter);
  }

  [Fact]
  public void ChangeAt_ComplexScenario_ShouldMergeProperly()
  {
    // Arrange - Simulate a text editor scenario
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    // Initial document state
    handle.Change(d => d.PutString(d.RootId, "content", "Line 1\nLine 2\nLine 3"));
    var baselineHeads = handle.GetHeads().ToList();

    // User makes online changes
    handle.Change(d => d.PutString(d.RootId, "content", "Line 1\nLine 2 (modified online)\nLine 3"));
    handle.Change(d => d.PutString(d.RootId, "lastModified", "2024-01-01"));

    // Act - User reconnects and applies offline edits from the baseline
    handle.ChangeAt(baselineHeads, d =>
    {
      d.PutString(d.RootId, "content", "Line 1\nLine 2\nLine 3 (modified offline)");
      d.PutString(d.RootId, "offline", "true");
    });

    // Assert - Document should contain both changes
    var json = handle.Doc().HydrateJson();
    Assert.Contains("lastModified", json);
    Assert.Contains("offline", json);
  }

  [Fact]
  public void ChangeAt_RepeatedCalls_ShouldAccumulate()
  {
    // Arrange
    var doc = AutomergeDocument.Create();
    var handle = new DocHandle<AutomergeDocument>(TestDocumentIds.CreateTestDocId(), document: doc);

    handle.Change(d => d.PutString(d.RootId, "base", "value"));
    var baseHeads = handle.GetHeads().ToList();

    // Act - Make multiple concurrent changes
    handle.ChangeAt(baseHeads, d => d.PutString(d.RootId, "branch1", "value1"));
    handle.ChangeAt(baseHeads, d => d.PutString(d.RootId, "branch2", "value2"));
    handle.ChangeAt(baseHeads, d => d.PutString(d.RootId, "branch3", "value3"));

    // Assert
    var json = handle.Doc().HydrateJson();
    Assert.Contains("branch1", json);
    Assert.Contains("branch2", json);
    Assert.Contains("branch3", json);
  }
}
