// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for cursor persistence: create cursor → save → load → resolve cursor →
/// verify position is correct after concurrent edits by a third party.
/// </summary>
[Trait("Category", "CursorPersistence")]
public sealed class AutomergeCursorPersistenceTests
{
  // ── Cursor Survives Save/Load Cycle ────────────────────────────────

  [Fact]
  public void Cursor_SurvivesSaveLoadCycle_ShouldResolveCorrectly()
  {
    // ARRANGE: Create document with a list
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "alpha");
    doc.ListInsertString(list, 1, "beta");
    doc.ListInsertString(list, 2, "gamma");
    doc.ListInsertString(list, 3, "delta");
    doc.Commit("init list");

    // Create cursor pointing at "beta" (index 1)
    var cursor = doc.GetCursor(list, 1);
    byte[] cursorBytes = cursor.ToBytes();

    // Save the document
    byte[] saved = doc.Save();

    // ACT: Load in a new document and resolve the cursor
    using var loaded = AutomergeDocument.Load(saved);
    var restoredCursor = AutomergeCursor.FromBytes(cursorBytes);
    var position = loaded.GetCursorPosition(list, restoredCursor);

    // ASSERT: Cursor should still point to index 1 ("beta")
    Assert.Equal(1UL, position);
    Assert.Equal("beta", loaded.ListGetString(list, position));
  }

  // ── Cursor Survives Save/Load + Concurrent Insert by Third Party ──

  [Fact]
  public void Cursor_SurvivesSaveLoadAndConcurrentInsert_ShouldTrackElement()
  {
    // ARRANGE: Create doc with text
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Commit("init text");

    // Create cursor at position 6 (the "W" in "World")
    var cursor = doc1.GetCursor(textId, 6);
    byte[] cursorBytes = cursor.ToBytes();

    // Save and create a third-party document (simulates another peer)
    byte[] saved = doc1.Save();
    using var doc3 = AutomergeDocument.Load(saved);

    // Third party inserts text BEFORE the cursor position
    doc3.SpliceText(textId, 0, 0, "Hey! ");
    doc3.Commit("third party insert before");

    // ACT: Load the original doc and merge the third party changes
    using var docLoaded = AutomergeDocument.Load(saved);
    docLoaded.Merge(doc3);

    // Resolve cursor in the merged document
    var restoredCursor = AutomergeCursor.FromBytes(cursorBytes);
    var position = docLoaded.GetCursorPosition(textId, restoredCursor);

    // ASSERT: "World" has shifted right by 5 chars ("Hey! ")
    // The cursor should now point to position 11 (6 + 5)
    var text = docLoaded.GetText(textId);
    Assert.Equal("Hey! Hello World", text);
    Assert.Equal(11UL, position);

    // The character at the cursor position should be 'W' from "World"
    Assert.Equal('W', text[(int)position]);
  }

  // ── Cursor Survives Save/Load + Concurrent Insert AFTER ────────────

  [Fact]
  public void Cursor_SurvivesSaveLoadAndConcurrentInsertAfter_ShouldStay()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var list = doc1.PutObject(root, "items", AutomergeContainerType.List);
    doc1.ListInsertString(list, 0, "A");
    doc1.ListInsertString(list, 1, "B");
    doc1.ListInsertString(list, 2, "C");
    doc1.Commit("init");

    // Cursor on "B" at index 1
    var cursor = doc1.GetCursor(list, 1);
    byte[] cursorBytes = cursor.ToBytes();
    byte[] saved = doc1.Save();

    // Third party inserts AFTER cursor position
    using var doc3 = AutomergeDocument.Load(saved);
    doc3.ListInsertString(list, 2, "X"); // Insert between B and C
    doc3.Commit("third party insert after");

    // Load original and merge
    using var loaded = AutomergeDocument.Load(saved);
    loaded.Merge(doc3);

    var restoredCursor = AutomergeCursor.FromBytes(cursorBytes);
    var position = loaded.GetCursorPosition(list, restoredCursor);

    // Cursor should still be at index 1 (insert was after)
    Assert.Equal(1UL, position);
    Assert.Equal("B", loaded.ListGetString(list, position));
  }

  // ── Cursor Survives Multiple Concurrent Edits from Multiple Peers ──

  [Fact]
  public void Cursor_SurvivesMultipleConcurrentPeers_ShouldTrackCorrectly()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    for(var i = 0; i < 10; i++)
    {
      doc.ListInsertString(list, (ulong)i, $"item{i}");
    }

    doc.Commit("init 10 items");

    // Cursor on "item5" at index 5
    var cursor = doc.GetCursor(list, 5);
    byte[] cursorBytes = cursor.ToBytes();
    byte[] saved = doc.Save();

    // Peer 2: inserts 3 items at the beginning
    using var peer2 = AutomergeDocument.Load(saved);
    peer2.ListInsertString(list, 0, "p2a");
    peer2.ListInsertString(list, 1, "p2b");
    peer2.ListInsertString(list, 2, "p2c");
    peer2.Commit("peer2 inserts");

    // Peer 3: removes item at index 0
    using var peer3 = AutomergeDocument.Load(saved);
    peer3.ListRemove(list, 0);
    peer3.Commit("peer3 removes first");

    // Merge both peers into original
    using var merged = AutomergeDocument.Load(saved);
    merged.Merge(peer2);
    merged.Merge(peer3);

    // Resolve cursor
    var restoredCursor = AutomergeCursor.FromBytes(cursorBytes);
    var position = merged.GetCursorPosition(list, restoredCursor);

    // The cursor should still point to "item5"
    Assert.Equal("item5", merged.ListGetString(list, position));
  }

  // ── Text Cursor Survives Save/Load + Concurrent Edits ──────────────

  [Fact]
  public void TextCursor_SurvivesSaveLoadAndConcurrentEdits_ShouldTrackPosition()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "The quick brown fox");
    doc1.Commit("init text");

    // Cursor at "brown" (position 10, the 'b')
    var cursor = doc1.GetCursor(textId, 10);
    byte[] cursorBytes = cursor.ToBytes();
    byte[] saved = doc1.Save();

    // Third party: inserts "very " before "quick"
    using var peer = AutomergeDocument.Load(saved);
    peer.SpliceText(textId, 4, 0, "very ");
    peer.Commit("peer inserts 'very '");

    // Load and merge
    using var loaded = AutomergeDocument.Load(saved);
    loaded.Merge(peer);

    var restoredCursor = AutomergeCursor.FromBytes(cursorBytes);
    var position = loaded.GetCursorPosition(textId, restoredCursor);

    // "brown" shifted right by 5 ("very "), so cursor should be at 15
    var text = loaded.GetText(textId);
    Assert.Equal("The very quick brown fox", text);
    Assert.Equal(15UL, position);
    Assert.Equal('b', text[(int)position]);
  }

  // ── Cursor Bytes Roundtrip Stability ───────────────────────────────

  [Fact]
  public void Cursor_BytesRoundtrip_ShouldBeIdempotent()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var list = doc.PutObject(root, "items", AutomergeContainerType.List);
    doc.ListInsertString(list, 0, "one");
    doc.ListInsertString(list, 1, "two");
    doc.ListInsertString(list, 2, "three");
    doc.Commit("init");

    var cursor = doc.GetCursor(list, 1);

    // Serialize → deserialize → serialize → deserialize
    byte[] bytes1 = cursor.ToBytes();
    var cursor2 = AutomergeCursor.FromBytes(bytes1);
    byte[] bytes2 = cursor2.ToBytes();
    var cursor3 = AutomergeCursor.FromBytes(bytes2);

    // All should resolve to the same position
    Assert.Equal(1UL, doc.GetCursorPosition(list, cursor));
    Assert.Equal(1UL, doc.GetCursorPosition(list, cursor2));
    Assert.Equal(1UL, doc.GetCursorPosition(list, cursor3));

    // Bytes should be identical
    Assert.Equal(bytes1, bytes2);
  }
}
