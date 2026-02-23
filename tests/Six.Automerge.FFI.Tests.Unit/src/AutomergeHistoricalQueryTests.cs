// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for historical queries (keys_at, length_at), iteration (values, values_at),
/// range queries (map_range, list_range), and object graph (parents, path_to_object).
/// </summary>
[Trait("Category", "HistoricalQuery")]
public sealed class AutomergeHistoricalQueryTests
{
  // ── keys_at ──────────────────────────────────────────────────────

  [Fact]
  public void GetKeysAtHeads_ShouldReturnKeysAtHistoricalPoint()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "alpha", "a");
    doc.PutString(root, "beta", "b");
    doc.Commit("first");
    var heads1 = doc.GetHeads();

    doc.PutString(root, "gamma", "c");
    doc.Commit("second");

    var keysAtV1 = doc.GetKeysAtHeads(root, heads1);
    var keysNow = doc.GetKeys(root);

    Assert.Equal(2, keysAtV1.Count);
    Assert.Contains("alpha", keysAtV1);
    Assert.Contains("beta", keysAtV1);
    Assert.Equal(3, keysNow.Count);
  }

  [Fact]
  public void GetKeysAtHeads_AfterKeyDeletion_ShouldExcludeDeletedKey()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "keep", "yes");
    doc.PutString(root, "remove", "no");
    doc.Commit("init");
    var headsBefore = doc.GetHeads();

    doc.Delete(root, "remove");
    doc.Commit("deleted");

    var keysAfterDelete = doc.GetKeys(root);
    var keysBeforeDelete = doc.GetKeysAtHeads(root, headsBefore);

    Assert.Single(keysAfterDelete);
    Assert.Equal(2, keysBeforeDelete.Count);
    Assert.Contains("remove", keysBeforeDelete);
  }

  [Fact]
  public void GetKeysAtHeads_EmptyMap_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.Commit("empty");
    var heads = doc.GetHeads();

    var keys = doc.GetKeysAtHeads(root, heads);

    Assert.Empty(keys);
  }

  // ── length_at ────────────────────────────────────────────────────

  [Fact]
  public void LengthAtHeads_MapAtHistoricalPoint_ShouldReturnCorrectCount()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "a", "1");
    doc.PutString(root, "b", "2");
    doc.Commit("two keys");
    var heads1 = doc.GetHeads();

    doc.PutString(root, "c", "3");
    doc.Commit("three keys");

    Assert.Equal(2UL, doc.LengthAtHeads(root, heads1));
    Assert.Equal(3UL, doc.ListLength(root));
  }

  [Fact]
  public void LengthAtHeads_ListAtHistoricalPoint_ShouldReturnCorrectCount()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "first");
    doc.ListInsertString(list, 1, "second");
    doc.Commit("two items");
    var heads1 = doc.GetHeads();

    doc.ListInsertString(list, 2, "third");
    doc.Commit("three items");

    Assert.Equal(2UL, doc.LengthAtHeads(list, heads1));
    Assert.Equal(3UL, doc.ListLength(list));
  }

  // ── values ───────────────────────────────────────────────────────

  [Fact]
  public void Values_MapWithScalars_ShouldReturnAllValues()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "name", "Alice");
    doc.PutInt64(root, "age", 30);
    doc.PutBoolean(root, "active", true);
    doc.Commit("data");

    var values = doc.Values(root);

    Assert.Equal(3, values.Count);
    Assert.Contains(values, v => !v.IsObject && v.Scalar?.StringValue == "Alice");
    Assert.Contains(values, v => !v.IsObject && v.Scalar?.IntValue == 30);
    Assert.Contains(values, v => !v.IsObject && v.Scalar?.BoolValue == true);
  }

  [Fact]
  public void Values_ListWithMixedTypes_ShouldReturnAllValues()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "hello");
    doc.ListInsertInt64(list, 1, 42);
    doc.ListInsertBoolean(list, 2, false);
    doc.Commit("list");

    var values = doc.Values(list);

    Assert.Equal(3, values.Count);
  }

  [Fact]
  public void Values_MapWithNestedObject_ShouldIncludeObjectEntry()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "name", "test");
    doc.PutObject(root, "nested", AutomergeContainerType.Map);
    doc.Commit("nested");

    var values = doc.Values(root);

    Assert.Equal(2, values.Count);
    Assert.Contains(values, v => v.IsObject && v.ObjectType == AutomergeContainerType.Map);
    Assert.Contains(values, v => !v.IsObject && v.Scalar?.StringValue == "test");
  }

  // ── values_at ────────────────────────────────────────────────────

  [Fact]
  public void ValuesAtHeads_ShouldReturnHistoricalValues()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "x", "old");
    doc.Commit("v1");
    var heads1 = doc.GetHeads();

    doc.PutString(root, "x", "new");
    doc.PutString(root, "y", "added");
    doc.Commit("v2");

    var valuesAtV1 = doc.ValuesAtHeads(root, heads1);
    var valuesNow = doc.Values(root);

    Assert.Single(valuesAtV1);
    Assert.Equal("old", valuesAtV1[0].Scalar?.StringValue);
    Assert.Equal(2, valuesNow.Count);
  }

  // ── map_range ────────────────────────────────────────────────────

  [Fact]
  public void MapRange_SubsetQuery_ShouldReturnEntriesInRange()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "apple", "1");
    doc.PutString(root, "banana", "2");
    doc.PutString(root, "cherry", "3");
    doc.PutString(root, "date", "4");
    doc.Commit("fruits");

    // Range "b".."d" should include "banana" and "cherry" (lexicographic)
    var range = doc.MapRange(root, "b", "d");

    Assert.Equal(2, range.Count);
  }

  [Fact]
  public void MapRangeFull_ShouldReturnAllEntries()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "x", "1");
    doc.PutString(root, "y", "2");
    doc.PutString(root, "z", "3");
    doc.Commit("data");

    var all = doc.MapRangeFull(root);

    Assert.Equal(3, all.Count);
  }

  [Fact]
  public void MapRange_EmptyRange_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    doc.PutString(root, "alpha", "1");
    doc.PutString(root, "beta", "2");
    doc.Commit("data");

    // Range "x".."z" should have no entries
    var range = doc.MapRange(root, "x", "z");

    Assert.Empty(range);
  }

  // ── list_range ───────────────────────────────────────────────────

  [Fact]
  public void ListRange_SubsetQuery_ShouldReturnEntriesInRange()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "zero");
    doc.ListInsertString(list, 1, "one");
    doc.ListInsertString(list, 2, "two");
    doc.ListInsertString(list, 3, "three");
    doc.ListInsertString(list, 4, "four");
    doc.Commit("list");

    // Range [1, 3) should return elements at index 1 and 2
    var range = doc.ListRange(list, 1, 3);

    Assert.Equal(2, range.Count);
  }

  [Fact]
  public void ListRangeFull_ShouldReturnAllEntries()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.Commit("list");

    var all = doc.ListRangeFull(list);

    Assert.Equal(3, all.Count);
  }

  [Fact]
  public void ListRange_EmptyRange_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.Commit("list");

    // Range [5, 10) is beyond list bounds
    var range = doc.ListRange(list, 5, 10);

    Assert.Empty(range);
  }

  // ── parents ──────────────────────────────────────────────────────

  [Fact]
  public void Parents_DeeplyNestedObject_ShouldReturnPathFromRoot()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var level1 = doc.PutObject(root, "level1", AutomergeContainerType.Map);
    var level2 = doc.PutObject(level1, "level2", AutomergeContainerType.Map);
    var level3 = doc.PutObject(level2, "level3", AutomergeContainerType.Map);
    doc.PutString(level3, "value", "deep");
    doc.Commit("nested");

    var path = doc.Parents(level3);

    // Path should be: root["level1"] -> level1["level2"] -> level2["level3"]
    Assert.Equal(3, path.Count);

    // First element: root -> "level1"
    var first = Assert.IsType<AutomergeMapPathElement>(path[0]);
    Assert.Equal("level1", first.Key);

    // Second element: level1 -> "level2"
    var second = Assert.IsType<AutomergeMapPathElement>(path[1]);
    Assert.Equal("level2", second.Key);

    // Third element: level2 -> "level3"
    var third = Assert.IsType<AutomergeMapPathElement>(path[2]);
    Assert.Equal("level3", third.Key);
  }

  [Fact]
  public void Parents_ObjectInList_ShouldIncludeListIndex()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertObject(list, 0, AutomergeContainerType.Map);
    var nested = doc.ListInsertObject(list, 1, AutomergeContainerType.Map);
    doc.PutString(nested, "key", "val");
    doc.Commit("list with objects");

    var path = doc.Parents(nested);

    // Path should be: root["items"] -> list[1]
    Assert.Equal(2, path.Count);

    var first = Assert.IsType<AutomergeMapPathElement>(path[0]);
    Assert.Equal("items", first.Key);

    var second = Assert.IsType<AutomergeListPathElement>(path[1]);
    Assert.Equal(1UL, second.Index);
  }

  [Fact]
  public void Parents_RootLevelObject_ShouldReturnSingleElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var child = doc.PutObject(root, "child", AutomergeContainerType.Map);
    doc.Commit("single level");

    var path = doc.Parents(child);

    Assert.Single(path);
    var elem = Assert.IsType<AutomergeMapPathElement>(path[0]);
    Assert.Equal("child", elem.Key);
  }

  // ── path_to_object ───────────────────────────────────────────────

  [Fact]
  public void PathToObject_VisibleObject_ShouldReturnSamePath()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var level1 = doc.PutObject(root, "a", AutomergeContainerType.Map);
    var level2 = doc.PutObject(level1, "b", AutomergeContainerType.Map);
    doc.PutString(level2, "c", "value");
    doc.Commit("nested");

    var path = doc.PathToObject(level2);

    Assert.Equal(2, path.Count);
    var first = Assert.IsType<AutomergeMapPathElement>(path[0]);
    Assert.Equal("a", first.Key);
    var second = Assert.IsType<AutomergeMapPathElement>(path[1]);
    Assert.Equal("b", second.Key);
  }

  [Fact]
  public void PathToObject_DeletedObject_ShouldReturnEmptyPath()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "list", AutomergeContainerType.List);
    var obj = doc.ListInsertObject(list, 0, AutomergeContainerType.Map);
    doc.PutString(obj, "val", "test");
    doc.Commit("created");

    // DeleteAsync the list item (makes the object invisible)
    doc.ListRemove(list, 0);
    doc.Commit("deleted");

    var path = doc.PathToObject(obj);

    // visible_path returns None for deleted objects → empty vec
    Assert.Empty(path);
  }

  [Fact]
  public void Parents_RoundTrip_ParentsAndPathToObjectMatch()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var map = doc.PutObject(root, "data", AutomergeContainerType.Map);
    var inner = doc.PutObject(map, "inner", AutomergeContainerType.Map);
    doc.PutString(inner, "value", "test");
    doc.Commit("nested");

    var parents = doc.Parents(inner);
    var pathTo = doc.PathToObject(inner);

    // For a visible object, parents.path() and visible_path() should return the same
    Assert.Equal(parents.Count, pathTo.Count);
    for(var i = 0; i < parents.Count; i++)
    {
      var p = Assert.IsType<AutomergeMapPathElement>(parents[i]);
      var t = Assert.IsType<AutomergeMapPathElement>(pathTo[i]);
      Assert.Equal(p.Key, t.Key);
    }
  }
}
