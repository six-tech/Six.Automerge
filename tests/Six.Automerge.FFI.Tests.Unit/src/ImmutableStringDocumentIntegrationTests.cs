using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for ImmutableString integration with AutomergeDocument.
/// </summary>
[Trait("Category", "ImmutableString")]
[Trait("Category", "Integration")]
public sealed class ImmutableStringDocumentIntegrationTests
{
  [Fact]
  public void PutImmutableString_WithValidValue_StoresSuccessfully()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var immutable = new ImmutableString("https://example.com");

    // Act
    doc.PutImmutableString(root, "url", immutable);
    doc.Commit("Add URL");

    // Assert - Verify via JSON hydration
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("https://example.com", parsed.RootElement.GetProperty("url").GetString());
  }

  [Fact]
  public void PutImmutableString_WithEmptyString_StoresSuccessfully()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var immutable = new ImmutableString("");

    // Act
    doc.PutImmutableString(root, "empty", immutable);
    doc.Commit("Add empty string");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("", parsed.RootElement.GetProperty("empty").GetString());
  }

  [Fact]
  public void PutImmutableString_WithUnicodeCharacters_PreservesValue()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var immutable = new ImmutableString("Hello 世界 🌍");

    // Act
    doc.PutImmutableString(root, "greeting", immutable);
    doc.Commit("Add greeting");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("Hello 世界 🌍", parsed.RootElement.GetProperty("greeting").GetString());
  }

  [Fact]
  public void PutImmutableString_WithWhitespaceOnly_PreservesValue()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var immutable = new ImmutableString("   ");

    // Act
    doc.PutImmutableString(root, "spaces", immutable);
    doc.Commit("Add spaces");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("   ", parsed.RootElement.GetProperty("spaces").GetString());
  }

  [Fact]
  public void PutImmutableString_MultipleValues_AllStoredCorrectly()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // Act
    doc.PutImmutableString(root, "url", new ImmutableString("https://example.com"));
    doc.PutImmutableString(root, "status", new ImmutableString("active"));
    doc.PutImmutableString(root, "id", new ImmutableString("user-123"));
    doc.Commit("Add multiple immutable strings");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    var rootElement = parsed.RootElement;
    Assert.Equal("https://example.com", rootElement.GetProperty("url").GetString());
    Assert.Equal("active", rootElement.GetProperty("status").GetString());
    Assert.Equal("user-123", rootElement.GetProperty("id").GetString());
  }

  [Fact]
  public void PutImmutableString_ImplicitConversion_Works()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var immutable = new ImmutableString("test");

    // Act - Should implicitly convert ImmutableString to string
    doc.PutImmutableString(root, "value", immutable);
    doc.Commit("Test implicit conversion");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("test", parsed.RootElement.GetProperty("value").GetString());
  }

  [Fact]
  public void PutImmutableString_SaveAndLoad_PreservesValue()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutImmutableString(root, "url", new ImmutableString("https://example.com"));
    doc.Commit("Add URL");

    // Act - Save and load
    var bytes = doc.Save();
    using var loaded = AutomergeDocument.Load(bytes);

    // Assert
    var json = loaded.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("https://example.com", parsed.RootElement.GetProperty("url").GetString());
  }

  [Fact]
  public void PutImmutableString_UpdateExistingValue_Replaces()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    doc.PutImmutableString(root, "status", new ImmutableString("pending"));
    doc.Commit("Initial status");

    // Act - Update
    doc.PutImmutableString(root, "status", new ImmutableString("approved"));
    doc.Commit("Update status");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("approved", parsed.RootElement.GetProperty("status").GetString());
  }

  [Fact]
  public void PutImmutableString_Interoperability_WorksWithGetString()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // Act - Store ImmutableString, read with JSON
    doc.PutImmutableString(root, "data", new ImmutableString("test value"));
    doc.Commit("Store ImmutableString");

    // Assert - Can read as plain string via JSON
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("test value", parsed.RootElement.GetProperty("data").GetString());
  }

  [Fact]
  public void PutImmutableString_LongString_StoresSuccessfully()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var longString = new string('a', 2000); // 2000 character string
    var immutable = new ImmutableString(longString);

    // Act
    doc.PutImmutableString(root, "long", immutable);
    doc.Commit("Add long string");

    // Assert
    var json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal(longString, parsed.RootElement.GetProperty("long").GetString());
  }

  [Fact]
  public void PutImmutableString_ConcurrentUpdates_LastWins()
  {
    // Arrange - Two documents
    using var docA = AutomergeDocument.Create();
    var rootA = docA.RootId;
    docA.PutImmutableString(rootA, "status", new ImmutableString("pending"));
    docA.Commit("Initial status");

    using var docB = AutomergeDocument.Create();
    var rootB = docB.RootId;
    var changesA = docA.GetChanges([]);
    docB.ApplyChanges(changesA);

    // Act - Concurrent updates
    docA.PutImmutableString(rootA, "status", new ImmutableString("approved"));
    docA.Commit("A: Approve");

    docB.PutImmutableString(rootB, "status", new ImmutableString("rejected"));
    docB.Commit("B: Reject");

    // Sync
    var changesB = docB.GetChanges(docA.GetHeads());
    docA.ApplyChanges(changesB);

    // Assert - One complete value wins (deterministic last-write-wins)
    var json = docA.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    var finalStatus = parsed.RootElement.GetProperty("status").GetString();

    // Must be either "approved" or "rejected", not a partial merge
    Assert.True(finalStatus == "approved" || finalStatus == "rejected",
      $"Expected complete value 'approved' or 'rejected', got '{finalStatus}'");

    // Must NOT be a character-level merge like "apprejectved"
    Assert.NotEqual("apprejectved", finalStatus);
  }

  [Fact]
  public void PutImmutableString_ThreeWayMerge_ConvergesToSameValue()
  {
    // Arrange - Three peers
    using var doc1 = AutomergeDocument.Create();
    var root1 = doc1.RootId;
    doc1.PutImmutableString(root1, "value", new ImmutableString("initial"));
    doc1.Commit("Initial");

    using var doc2 = AutomergeDocument.Create();
    using var doc3 = AutomergeDocument.Create();

    var changes1 = doc1.GetChanges([]);
    doc2.ApplyChanges(changes1);
    doc3.ApplyChanges(changes1);

    // Act - Each peer makes different update
    doc1.PutImmutableString(doc1.RootId, "value", new ImmutableString("value-A"));
    doc1.Commit("Update from A");

    doc2.PutImmutableString(doc2.RootId, "value", new ImmutableString("value-B"));
    doc2.Commit("Update from B");

    doc3.PutImmutableString(doc3.RootId, "value", new ImmutableString("value-C"));
    doc3.Commit("Update from C");

    // Sync all peers
    var changes1to2 = doc1.GetChanges(doc2.GetHeads());
    var changes1to3 = doc1.GetChanges(doc3.GetHeads());
    var changes2to1 = doc2.GetChanges(doc1.GetHeads());
    var changes2to3 = doc2.GetChanges(doc3.GetHeads());
    var changes3to1 = doc3.GetChanges(doc1.GetHeads());
    var changes3to2 = doc3.GetChanges(doc2.GetHeads());

    doc2.ApplyChanges(changes1to2);
    doc2.ApplyChanges(changes3to2);
    doc3.ApplyChanges(changes1to3);
    doc3.ApplyChanges(changes2to3);
    doc1.ApplyChanges(changes2to1);
    doc1.ApplyChanges(changes3to1);

    // Assert - All peers converged to same value
    var json1 = doc1.HydrateJson();
    var json2 = doc2.HydrateJson();
    var json3 = doc3.HydrateJson();

    using var parsed1 = JsonDocument.Parse(json1);
    using var parsed2 = JsonDocument.Parse(json2);
    using var parsed3 = JsonDocument.Parse(json3);

    var value1 = parsed1.RootElement.GetProperty("value").GetString();
    var value2 = parsed2.RootElement.GetProperty("value").GetString();
    var value3 = parsed3.RootElement.GetProperty("value").GetString();

    // All peers must have the same final value
    Assert.Equal(value1, value2);
    Assert.Equal(value2, value3);

    // Value must be one of the complete updates, not a merge
    Assert.True(value1 == "value-A" || value1 == "value-B" || value1 == "value-C",
      $"Expected complete value, got '{value1}'");
  }
}
