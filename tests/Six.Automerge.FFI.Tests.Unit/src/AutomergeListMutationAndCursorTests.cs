// Copyright (c) Six. Licensed under the MIT License.

using System.Text.Json;

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for list_splice, list_put, cursor create/resolve, and cursor stability
/// under concurrent edits.
/// </summary>
[Trait("Category", "ListMutationCursor")]
public sealed class AutomergeListMutationAndCursorTests
{
  // ── ListSplice: Insert Multiple ───────────────────────────────────

  [Fact]
  public void ListSplice_InsertMultipleValues_ShouldAddAllElements()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);

    var values = new[]
    {
      new AutomergeScalarValue(AutomergeScalarValueKind.String, stringValue: "alpha"),
      new AutomergeScalarValue(AutomergeScalarValueKind.Int, intValue: 42),
      new AutomergeScalarValue(AutomergeScalarValueKind.Boolean, boolValue: true)
    };

    doc.ListSplice(list, 0, 0, values);
    doc.Commit("splice insert");

    Assert.Equal(3UL, doc.ListLength(list));
    Assert.Equal("alpha", doc.ListGetString(list, 0));
    Assert.Equal(42L, doc.ListGetInt64(list, 1));
    Assert.Equal(true, doc.ListGetBoolean(list, 2));
  }

  [Fact]
  public void ListSplice_InsertIntoMiddle_ShouldShiftExistingElements()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "first");
    doc.ListInsertString(list, 1, "last");
    doc.Commit("init");

    var values = new[]
    {
      new AutomergeScalarValue(AutomergeScalarValueKind.String, stringValue: "middle1"),
      new AutomergeScalarValue(AutomergeScalarValueKind.String, stringValue: "middle2")
    };

    doc.ListSplice(list, 1, 0, values);
    doc.Commit("splice middle");

    Assert.Equal(4UL, doc.ListLength(list));
    Assert.Equal("first", doc.ListGetString(list, 0));
    Assert.Equal("middle1", doc.ListGetString(list, 1));
    Assert.Equal("middle2", doc.ListGetString(list, 2));
    Assert.Equal("last", doc.ListGetString(list, 3));
  }

  // ── ListSplice: DeleteAsync + Insert ───────────────────────────────────

  [Fact]
  public void ListSplice_DeleteAndInsert_ShouldReplaceElements()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.ListInsertString(list, 3, "d");
    doc.Commit("init");

    var values = new[]
    {
      new AutomergeScalarValue(AutomergeScalarValueKind.String, stringValue: "X"),
      new AutomergeScalarValue(AutomergeScalarValueKind.String, stringValue: "Y")
    };

    doc.ListSplice(list, 1, 2, values);
    doc.Commit("splice replace");

    Assert.Equal(4UL, doc.ListLength(list));
    Assert.Equal("a", doc.ListGetString(list, 0));
    Assert.Equal("X", doc.ListGetString(list, 1));
    Assert.Equal("Y", doc.ListGetString(list, 2));
    Assert.Equal("d", doc.ListGetString(list, 3));
  }

  [Fact]
  public void ListSplice_DeleteOnly_ShouldRemoveElements()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "keep");
    doc.ListInsertString(list, 1, "remove1");
    doc.ListInsertString(list, 2, "remove2");
    doc.ListInsertString(list, 3, "keep2");
    doc.Commit("init");

    doc.ListSplice(list, 1, 2, Array.Empty<AutomergeScalarValue>());
    doc.Commit("splice delete");

    Assert.Equal(2UL, doc.ListLength(list));
    Assert.Equal("keep", doc.ListGetString(list, 0));
    Assert.Equal("keep2", doc.ListGetString(list, 1));
  }

  // ── ListPut: Replace Element ──────────────────────────────────────

  [Fact]
  public void ListPutString_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "original");
    doc.Commit("init");

    doc.ListPutString(list, 0, "replaced");
    doc.Commit("put");

    Assert.Equal("replaced", doc.ListGetString(list, 0));
    Assert.Equal(1UL, doc.ListLength(list));
  }

  [Fact]
  public void ListPutInt64_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertInt64(list, 0, 100);
    doc.Commit("init");

    doc.ListPutInt64(list, 0, 999);
    doc.Commit("put");

    Assert.Equal(999L, doc.ListGetInt64(list, 0));
  }

  [Fact]
  public void ListPutBoolean_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertBoolean(list, 0, false);
    doc.Commit("init");

    doc.ListPutBoolean(list, 0, true);
    doc.Commit("put");

    Assert.Equal(true, doc.ListGetBoolean(list, 0));
  }

  [Fact]
  public void ListPutDouble_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertDouble(list, 0, 1.5);
    doc.Commit("init");

    doc.ListPutDouble(list, 0, 3.14);
    doc.Commit("put");

    Assert.Equal(3.14, doc.ListGetDouble(list, 0));
  }

  [Fact]
  public void ListPutUInt64_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertUInt64(list, 0, 10UL);
    doc.Commit("init");

    doc.ListPutUInt64(list, 0, 999UL);
    doc.Commit("put");

    Assert.Equal(999UL, doc.ListGetUInt64(list, 0));
  }

  [Fact]
  public void ListPutBytes_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertBytes(list, 0, new byte[] { 1, 2, 3 });
    doc.Commit("init");

    byte[] newBytes = [4, 5, 6, 7];
    doc.ListPutBytes(list, 0, newBytes);
    doc.Commit("put");

    Assert.Equal(newBytes, doc.ListGetBytes(list, 0));
  }

  [Fact]
  public void ListPutNull_ShouldReplaceExistingElement()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "something");
    doc.Commit("init");

    doc.ListPutNull(list, 0);
    doc.Commit("put null");

    // After putting null, string get should return null
    Assert.Null(doc.ListGetString(list, 0));
    Assert.Equal(1UL, doc.ListLength(list));
  }

  [Fact]
  public void ListPutObject_ShouldReplaceWithNestedContainer()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "placeholder");
    doc.Commit("init");

    var nested = doc.ListPutObject(list, 0, AutomergeContainerType.Map);
    doc.PutString(nested, "key", "value");
    doc.Commit("put object");

    var retrieved = doc.ListGetObject(list, 0);
    Assert.NotNull(retrieved);
    Assert.Equal("value", doc.GetString(retrieved!, "key"));
  }

  // ── Cursor: Create and Resolve ────────────────────────────────────

  [Fact]
  public void GetCursor_AndResolvePosition_ShouldReturnOriginalIndex()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.Commit("init");

    var cursor = doc.GetCursor(list, 1);

    var position = doc.GetCursorPosition(list, cursor);
    Assert.Equal(1UL, position);
  }

  [Fact]
  public void GetCursor_OnTextObject_ShouldWork()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var text = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(text, 0, 0, "Hello World");
    doc.Commit("init");

    var cursor = doc.GetCursor(text, 5);

    var position = doc.GetCursorPosition(text, cursor);
    Assert.Equal(5UL, position);
  }

  // ── Cursor: Survives Concurrent Insert Before Position ────────────

  [Fact]
  public void Cursor_SurvivesConcurrentInsertBefore_ShouldTrackForward()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.Commit("init");

    // Create cursor pointing at "b" (index 1)
    var cursor = doc.GetCursor(list, 1);

    // Insert element BEFORE the cursor position
    doc.ListInsertString(list, 0, "z");
    doc.Commit("insert before");

    // Cursor should now resolve to index 2 (shifted by 1)
    var position = doc.GetCursorPosition(list, cursor);
    Assert.Equal(2UL, position);

    // Verify "b" is still at the cursor position
    Assert.Equal("b", doc.ListGetString(list, position));
  }

  // ── Cursor: Survives Concurrent Insert After Position ─────────────

  [Fact]
  public void Cursor_SurvivesConcurrentInsertAfter_ShouldStayAtSameIndex()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.Commit("init");

    // Create cursor pointing at "b" (index 1)
    var cursor = doc.GetCursor(list, 1);

    // Insert element AFTER the cursor position
    doc.ListInsertString(list, 2, "z");
    doc.Commit("insert after");

    // Cursor should still resolve to index 1
    var position = doc.GetCursorPosition(list, cursor);
    Assert.Equal(1UL, position);

    // Verify "b" is still at the cursor position
    Assert.Equal("b", doc.ListGetString(list, position));
  }

  // ── Cursor: Persistence (Encode/Decode Bytes) ─────────────────────

  [Fact]
  public void Cursor_ToBytes_FromBytes_ShouldPreservePosition()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.Commit("init");

    var cursor = doc.GetCursor(list, 1);

    // Serialize to bytes
    byte[] bytes = cursor.ToBytes();
    Assert.NotEmpty(bytes);

    // Deserialize from bytes
    var restored = AutomergeCursor.FromBytes(bytes);

    // Should resolve to same position
    var position = doc.GetCursorPosition(list, restored);
    Assert.Equal(1UL, position);
  }

  [Fact]
  public void Cursor_ToBytes_ShouldBeStableAcrossDocuments()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "x");
    doc.ListInsertString(list, 1, "y");
    doc.ListInsertString(list, 2, "z");
    doc.Commit("init");

    var cursor = doc.GetCursor(list, 2);
    byte[] cursorBytes = cursor.ToBytes();

    // Save and reload the document
    byte[] saved = doc.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    // Restore cursor in the loaded document
    var restored = AutomergeCursor.FromBytes(cursorBytes);
    var position = doc2.GetCursorPosition(list, restored);
    Assert.Equal(2UL, position);
  }

  // ── Cursor: Survives Concurrent Insert from Another Peer ──────────

  [Fact]
  public void Cursor_SurvivesMergeWithConcurrentInsert_ShouldTrackCorrectly()
  {
    using var doc1 = AutomergeDocument.Create("aa");
    var root1 = doc1.RootId;

    var list = doc1.PutObject(root1, "items", AutomergeContainerType.List);
    doc1.ListInsertString(list, 0, "a");
    doc1.ListInsertString(list, 1, "b");
    doc1.ListInsertString(list, 2, "c");
    doc1.Commit("init");

    // Fork to create a second peer
    using var doc2 = doc1.Fork();

    // Create cursor in doc1 pointing at "b" (index 1)
    var cursor = doc1.GetCursor(list, 1);
    byte[] cursorBytes = cursor.ToBytes();

    // Doc2 inserts an element at index 0 (before "b")
    doc2.ListInsertString(list, 0, "z");
    doc2.Commit("peer insert");

    // Merge changes
    doc1.Merge(doc2);

    // Cursor should still track "b" — now at a new index
    var restored = AutomergeCursor.FromBytes(cursorBytes);
    var position = doc1.GetCursorPosition(list, restored);

    // "b" has shifted from index 1 to index 2 due to insert at 0
    Assert.Equal("b", doc1.ListGetString(list, position));
  }

  // ── ListSplice: With Different Scalar Types ───────────────────────

  [Fact]
  public void ListSplice_WithMixedTypes_ShouldInsertAllCorrectly()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "data", AutomergeContainerType.List);

    var values = new[]
    {
      new AutomergeScalarValue(AutomergeScalarValueKind.String, stringValue: "text"),
      new AutomergeScalarValue(AutomergeScalarValueKind.Int, intValue: -10),
      new AutomergeScalarValue(AutomergeScalarValueKind.Uint, uintValue: 999UL),
      new AutomergeScalarValue(AutomergeScalarValueKind.Double, doubleValue: 2.718),
      new AutomergeScalarValue(AutomergeScalarValueKind.Boolean, boolValue: false),
      new AutomergeScalarValue(AutomergeScalarValueKind.Null)
    };

    doc.ListSplice(list, 0, 0, values);
    doc.Commit("mixed splice");

    Assert.Equal(6UL, doc.ListLength(list));
    Assert.Equal("text", doc.ListGetString(list, 0));
    Assert.Equal(-10L, doc.ListGetInt64(list, 1));
    Assert.Equal(999UL, doc.ListGetUInt64(list, 2));
    Assert.Equal(2.718, doc.ListGetDouble(list, 3));
    Assert.Equal(false, doc.ListGetBoolean(list, 4));
    // Null: string accessor returns null
    Assert.Null(doc.ListGetString(list, 5));
  }

  // ── ListSplice: Empty Values (DeleteAsync Only) ────────────────────────

  [Fact]
  public void ListSplice_EmptyValues_ShouldOnlyDelete()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "a");
    doc.ListInsertString(list, 1, "b");
    doc.ListInsertString(list, 2, "c");
    doc.Commit("init");

    doc.ListSplice(list, 0, 1, Array.Empty<AutomergeScalarValue>());
    doc.Commit("delete first");

    Assert.Equal(2UL, doc.ListLength(list));
    Assert.Equal("b", doc.ListGetString(list, 0));
    Assert.Equal("c", doc.ListGetString(list, 1));
  }

  // ── ListSplice: Verified via Hydrate ──────────────────────────────

  [Fact]
  public void ListSplice_ResultShouldBeVisibleInHydratedJson()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "numbers", AutomergeContainerType.List);

    var values = new[]
    {
      new AutomergeScalarValue(AutomergeScalarValueKind.Int, intValue: 10),
      new AutomergeScalarValue(AutomergeScalarValueKind.Int, intValue: 20),
      new AutomergeScalarValue(AutomergeScalarValueKind.Int, intValue: 30)
    };

    doc.ListSplice(list, 0, 0, values);
    doc.Commit("add numbers");

    string json = doc.HydrateJson();
    using var parsed = JsonDocument.Parse(json);
    var numbersArray = parsed.RootElement.GetProperty("numbers");
    Assert.Equal(3, numbersArray.GetArrayLength());
    Assert.Equal(10, numbersArray[0].GetInt32());
    Assert.Equal(20, numbersArray[1].GetInt32());
    Assert.Equal(30, numbersArray[2].GetInt32());
  }
}
