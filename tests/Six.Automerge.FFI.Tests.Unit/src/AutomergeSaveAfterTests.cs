using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using Six.Automerge;

namespace Six.Automerge.FFI.Test.Unit;

/// <summary>
/// Tests for SaveAfter API - incremental saves relative to explicit heads.
/// </summary>
[Trait("Category", "Persistence")]
public class AutomergeSaveAfterTests
{
  [Fact]
  public void SaveAfter_WithBaseHeads_ShouldReturnOnlyNewChanges()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "key1", "value1");
    doc.Commit("first change");
    var baseHeads = doc.GetHeads();
    var baseData = doc.Save();

    // Act
    doc.PutString(root, "key2", "value2");
    doc.Commit("second change");
    var incremental = doc.SaveAfter(baseHeads);

    // Assert
    Assert.NotEmpty(incremental);

    // Verify: base document should have only key1
    using var baseDoc = AutomergeDocument.Load(baseData);
    var baseJson = baseDoc.HydrateJson();
    using var baseParsed = JsonDocument.Parse(baseJson);
    Assert.True(baseParsed.RootElement.TryGetProperty("key1", out _));
    Assert.False(baseParsed.RootElement.TryGetProperty("key2", out _));

    // Verify: base + incremental should have both keys
    baseDoc.LoadIncremental(incremental);
    var finalJson = baseDoc.HydrateJson();
    using var finalParsed = JsonDocument.Parse(finalJson);
    Assert.True(finalParsed.RootElement.TryGetProperty("key1", out var key1));
    Assert.Equal("value1", key1.GetString());
    Assert.True(finalParsed.RootElement.TryGetProperty("key2", out var key2));
    Assert.Equal("value2", key2.GetString());
  }

  [Fact]
  public void SaveAfter_WithEmptyDocument_ShouldReturnAllChanges()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;
    var emptyHeads = doc.GetHeads();

    doc.PutString(root, "key", "value");
    doc.Commit("add data");

    // Act - SaveAfter from empty heads should return all changes
    var allChanges = doc.SaveAfter(emptyHeads);

    // Assert
    Assert.NotEmpty(allChanges);

    // Verify it reconstructs the full document
    using var newDoc = AutomergeDocument.Create();
    newDoc.LoadIncremental(allChanges);
    var json = newDoc.HydrateJson();

    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("value", parsed.RootElement.GetProperty("key").GetString());
  }

  [Fact]
  public void SaveAfter_RoundTrip_ShouldReconstruct()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "name", "Alice");
    doc.PutInt64(root, "age", 30);
    doc.Commit("initial data");

    var baseHeads = doc.GetHeads();
    var baseData = doc.Save();

    doc.PutString(root, "city", "Seattle");
    doc.PutInt64(root, "age", 31);
    doc.Commit("update data");

    // Act
    var incremental = doc.SaveAfter(baseHeads);

    // Assert - reconstruct from base + incremental
    using var restored = AutomergeDocument.Load(baseData);
    restored.LoadIncremental(incremental);

    var originalJson = doc.HydrateJson();
    var restoredJson = restored.HydrateJson();

    Assert.Equal(originalJson, restoredJson);
  }

  [Fact]
  public void SaveAfter_WithCurrentHeads_ShouldReturnEmpty()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "key", "value");
    doc.Commit("change");

    var currentHeads = doc.GetHeads();

    // Act - SaveAfter with current heads (no new changes)
    var result = doc.SaveAfter(currentHeads);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public void SaveAfter_WithInvalidHeads_ShouldThrow()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "key", "value");
    doc.Commit();

    var invalidHeads = new[] { "not-a-valid-hex-hash" };

    // Act & Assert
    Assert.Throws<ArgumentException>(() => doc.SaveAfter(invalidHeads));
  }

  [Fact]
  public void SaveAfter_WithNullHeads_ShouldThrow()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();

    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => doc.SaveAfter(null!));
  }

  [Fact]
  public void SaveAfter_WithEmptyHeadsArray_ShouldSaveAllChanges()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "key", "value");
    doc.Commit("add data");

    // Act - Empty heads array should save all changes (same as SaveAfter with genesis heads)
    var allChanges = doc.SaveAfter(Array.Empty<string>());

    // Assert
    Assert.NotEmpty(allChanges);

    // Verify it reconstructs the full document
    using var newDoc = AutomergeDocument.Create();
    newDoc.LoadIncremental(allChanges);
    var json = newDoc.HydrateJson();

    using var parsed = JsonDocument.Parse(json);
    Assert.Equal("value", parsed.RootElement.GetProperty("key").GetString());
  }

  [Fact]
  public void SaveAfter_MultipleIncrements_ShouldChain()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // Change 1
    doc.PutInt64(root, "count", 1);
    doc.Commit("change 1");
    var heads1 = doc.GetHeads();
    var save1 = doc.Save();

    // Change 2
    doc.PutInt64(root, "count", 2);
    doc.Commit("change 2");
    var increment1 = doc.SaveAfter(heads1);
    var heads2 = doc.GetHeads();

    // Change 3
    doc.PutInt64(root, "count", 3);
    doc.Commit("change 3");
    var increment2 = doc.SaveAfter(heads2);

    // Act - Load base and apply increments
    using var restored = AutomergeDocument.Load(save1);
    restored.LoadIncremental(increment1);
    restored.LoadIncremental(increment2);

    // Assert
    var json = restored.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal(3, parsed.RootElement.GetProperty("count").GetInt64());
  }

  [Fact]
  public void SaveAfter_ComparedToSaveIncremental_ShouldProduceSameResult()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "key", "value1");
    doc.Commit("first");
    var heads1 = doc.GetHeads();

    // SaveIncremental establishes a cursor at current position
    var _ = doc.SaveIncremental();

    // Make second change
    doc.PutString(root, "key", "value2");
    doc.Commit("second");

    // Act - Both should capture the same changes
    var incremental1 = doc.SaveIncremental(); // Uses internal cursor (after first SaveIncremental)
    var incremental2 = doc.SaveAfter(heads1); // Uses explicit heads (same as cursor position)

    // Assert - both should produce equivalent results
    // Note: The byte arrays should be identical since they're from the same document
    Assert.Equal(incremental1, incremental2);
  }

  [Fact]
  public void SaveAfter_LargeDocument_ShouldComplete()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    // Create a large document with 1000+ operations
    for(var i = 0; i < 1000; i++)
    {
      doc.PutInt64(root, $"key{i}", i);
    }
    doc.Commit("bulk insert");

    var baseHeads = doc.GetHeads();
    var fullSize = doc.Save().Length;

    // Act - Add 100 more operations
    for(var i = 1000; i < 1100; i++)
    {
      doc.PutInt64(root, $"key{i}", i);
    }
    doc.Commit("incremental insert");

    var incremental = doc.SaveAfter(baseHeads);
    var incrementalSize = incremental.Length;

    // Assert - incremental should be much smaller than full save
    Assert.NotEmpty(incremental);
    Assert.True(incrementalSize < fullSize / 2,
      $"Incremental size ({incrementalSize}) should be much smaller than full size ({fullSize})");

    // Verify correctness
    using var restored = AutomergeDocument.Load(doc.Save());
    restored.Isolate(baseHeads);
    restored.Integrate();
    restored.LoadIncremental(incremental);

    var json = restored.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    Assert.Equal(1099, parsed.RootElement.GetProperty("key1099").GetInt64());
  }

  [Fact]
  public void SaveAfter_WithTooShortHash_ShouldThrow()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    doc.PutString(doc.RootId, "key", "value");
    doc.Commit();

    var tooShortHash = new[] { "0000000000000000000000000000000000000000000000000000000000000" }; // 63 chars

    // Act & Assert
    Assert.Throws<ArgumentException>(() => doc.SaveAfter(tooShortHash));
  }

  [Fact]
  public void SaveAfter_WithMixedValidAndInvalidHeads_ShouldThrow()
  {
    // Arrange
    using var doc = AutomergeDocument.Create();
    doc.PutString(doc.RootId, "key", "value");
    var validHash = doc.Commit();

    var mixedHeads = new[]
    {
      validHash!,
      "invalid-hash"
    };

    // Act & Assert
    var exception = Assert.Throws<ArgumentException>(() => doc.SaveAfter(mixedHeads));
    Assert.Contains("index 1", exception.Message);
  }
}
