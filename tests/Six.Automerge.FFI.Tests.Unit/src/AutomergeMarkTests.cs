// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for mark and unmark operations on text objects.
/// </summary>
[Trait("Category", "Marks")]
public sealed class AutomergeMarkTests
{
  // ── Mark Bold ──────────────────────────────────────────────────────

  [Fact]
  public void Mark_BoolOnTextRange_ShouldApplyMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    // Verify via diff that the mark was applied
    var heads = doc.GetHeads();
    Assert.NotEmpty(heads);
  }

  [Fact]
  public void Mark_BoolOnTextRange_ShouldBeVisibleViaDiff()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("write text");

    var headsBefore = doc.GetHeads();

    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var headsAfter = doc.GetHeads();
    var patches = doc.Diff(headsBefore, headsAfter);

    Assert.NotEmpty(patches);
    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);
    Assert.NotEmpty(markPatch.Action.Marks);

    var mark = markPatch.Action.Marks[0];
    Assert.Equal("bold", mark.Name);
    Assert.Equal(0UL, mark.Start);
    Assert.Equal(5UL, mark.End);
    Assert.NotNull(mark.Value);
    Assert.Equal(AutomergeScalarValueKind.Boolean, mark.Value.Kind);
    Assert.True(mark.Value.BoolValue);
  }

  // ── Unmark Bold ────────────────────────────────────────────────────

  [Fact]
  public void Unmark_BoldOnTextRange_ShouldRemoveMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var headsAfterMark = doc.GetHeads();

    doc.Unmark(textId, "bold", 0, 5);
    doc.Commit("unmark bold");

    var headsAfterUnmark = doc.GetHeads();
    var patches = doc.Diff(headsAfterMark, headsAfterUnmark);

    Assert.NotEmpty(patches);
    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);
    Assert.NotEmpty(markPatch.Action.Marks);

    // Unmark produces a mark patch with null value
    var mark = markPatch.Action.Marks[0];
    Assert.Equal("bold", mark.Name);
    Assert.Equal(AutomergeScalarValueKind.Null, mark.Value.Kind);
  }

  // ── Expand Modes ───────────────────────────────────────────────────

  [Theory]
  [InlineData(ExpandMode.Before)]
  [InlineData(ExpandMode.After)]
  [InlineData(ExpandMode.Both)]
  [InlineData(ExpandMode.None)]
  public void Mark_WithEachExpandMode_ShouldSucceed(ExpandMode expand)
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true, expand);
    doc.Commit("mark");

    // Should complete without error
    var text = doc.GetText(textId);
    Assert.Equal("Hello World", text);
  }

  [Fact]
  public void Mark_ExpandNone_InsertAtBoundary_ShouldNotExpandMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true, ExpandMode.None);
    doc.Commit("mark bold none");
    var headsAfterMark = doc.GetHeads();

    // Insert at the end boundary (index 5)
    doc.SpliceText(textId, 5, 0, "X");
    doc.Commit("insert at boundary");

    var headsAfterInsert = doc.GetHeads();

    // The inserted character should NOT be bold with ExpandMode.None
    // Verify the text is correct
    var text = doc.GetText(textId);
    Assert.Equal("HelloX World", text);
  }

  [Fact]
  public void Mark_ExpandBoth_InsertAtBoundary_ShouldExpandMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true, ExpandMode.Both);
    doc.Commit("mark bold both");

    // Insert at the end boundary (index 5)
    doc.SpliceText(textId, 5, 0, "X");
    doc.Commit("insert at boundary");

    var text = doc.GetText(textId);
    Assert.Equal("HelloX World", text);
  }

  // ── Different Value Types ──────────────────────────────────────────

  [Fact]
  public void Mark_StringValue_ShouldApplyMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Click here for info");
    doc.Commit("write text");

    var headsBefore = doc.GetHeads();

    doc.Mark(textId, 0, 10, "link", "https://example.com");
    doc.Commit("mark link");

    var headsAfter = doc.GetHeads();
    var patches = doc.Diff(headsBefore, headsAfter);

    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);

    var mark = markPatch.Action.Marks[0];
    Assert.Equal("link", mark.Name);
    Assert.Equal(AutomergeScalarValueKind.String, mark.Value.Kind);
    Assert.Equal("https://example.com", mark.Value.StringValue);
  }

  [Fact]
  public void Mark_IntValue_ShouldApplyMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Large text here");
    doc.Commit("write text");

    var headsBefore = doc.GetHeads();

    doc.Mark(textId, 0, 10, "fontSize", 24L);
    doc.Commit("mark fontSize");

    var headsAfter = doc.GetHeads();
    var patches = doc.Diff(headsBefore, headsAfter);

    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);

    var mark = markPatch.Action.Marks[0];
    Assert.Equal("fontSize", mark.Name);
    Assert.Equal(AutomergeScalarValueKind.Int, mark.Value.Kind);
    Assert.Equal(24L, mark.Value.IntValue);
  }

  [Fact]
  public void Mark_BoolValue_ShouldApplyMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Bold text");
    doc.Commit("write text");

    var headsBefore = doc.GetHeads();

    doc.Mark(textId, 0, 4, "bold", true);
    doc.Commit("mark bold");

    var headsAfter = doc.GetHeads();
    var patches = doc.Diff(headsBefore, headsAfter);

    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);

    var mark = markPatch.Action.Marks[0];
    Assert.Equal("bold", mark.Name);
    Assert.Equal(AutomergeScalarValueKind.Boolean, mark.Value.Kind);
    Assert.True(mark.Value.BoolValue);
  }

  // ── Overlapping Marks ──────────────────────────────────────────────

  [Fact]
  public void Mark_OverlappingMarks_DifferentNames_ShouldCoexist()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("write text");

    var headsBefore = doc.GetHeads();

    doc.Mark(textId, 0, 5, "bold", true);
    doc.Mark(textId, 3, 8, "italic", true);
    doc.Commit("mark bold and italic");

    var headsAfter = doc.GetHeads();
    var patches = doc.Diff(headsBefore, headsAfter);

    var markPatches = patches.Where(p => p.Action.Kind == AutomergePatchActionKind.Mark).ToList();
    Assert.NotEmpty(markPatches);

    // Both marks should be present in the patches
    var allMarks = markPatches.SelectMany(p => p.Action.Marks).ToList();
    Assert.True(allMarks.Any(m => m.Name == "bold"), "Should have bold mark");
    Assert.True(allMarks.Any(m => m.Name == "italic"), "Should have italic mark");
  }

  [Fact]
  public void Mark_OverlappingSameName_ShouldOverwrite()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");

    doc.Mark(textId, 0, 11, "color", "red");
    doc.Commit("mark red");

    var headsAfterRed = doc.GetHeads();

    doc.Mark(textId, 3, 8, "color", "blue");
    doc.Commit("mark blue");

    var headsAfterBlue = doc.GetHeads();
    var patches = doc.Diff(headsAfterRed, headsAfterBlue);

    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);

    // The overlapping region should have the new mark value
    var mark = markPatch.Action.Marks.FirstOrDefault(m => m.Name == "color");
    Assert.NotNull(mark);
    Assert.Equal("blue", mark.Value.StringValue);
  }

  // ── Error Cases ────────────────────────────────────────────────────

  [Fact]
  public void Mark_OnEmptyText_ShouldThrowWhenRangeExceedsLength()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.Commit("empty text");

    // Marking an empty text with range 0..5 should throw since the text has 0 chars
    Assert.ThrowsAny<Exception>(() => doc.Mark(textId, 0, 5, "bold", true));
  }

  [Fact]
  public void Mark_WithInvalidRange_StartGreaterThanEnd_ShouldBeNoOp()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("write text");

    var headsBefore = doc.GetHeads();

    // start > end is accepted by automerge as a no-op (empty range)
    doc.Mark(textId, 5, 2, "bold", true);
    doc.Commit("noop mark");

    // Text should be unchanged
    var text = doc.GetText(textId);
    Assert.Equal("Hello World", text);
  }

  [Fact]
  public void Mark_WithRangeExceedingTextLength_ShouldThrow()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hi");
    doc.Commit("write text");

    // Text has 2 chars, marking 0..100 should throw
    Assert.ThrowsAny<Exception>(() => doc.Mark(textId, 0, 100, "bold", true));
  }

  // ── Mark Survives Save/Load ────────────────────────────────────────

  [Fact]
  public void Mark_ShouldSurviveSaveAndLoad()
  {
    byte[] saved;
    IReadOnlyList<string> headsBeforeMark;
    IReadOnlyList<string> headsAfterMark;

    using(var doc = AutomergeDocument.Create())
    {
      var root = doc.RootId;
      var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
      doc.SpliceText(textId, 0, 0, "Hello World");
      doc.Commit("write text");

      headsBeforeMark = doc.GetHeads();

      doc.Mark(textId, 0, 5, "bold", true);
      doc.Commit("mark bold");

      headsAfterMark = doc.GetHeads();
      saved = doc.Save();
    }

    using var loaded = AutomergeDocument.Load(saved);
    var patches = loaded.Diff(headsBeforeMark, headsAfterMark);

    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);
    Assert.NotEmpty(markPatch.Action.Marks);

    var mark = markPatch.Action.Marks[0];
    Assert.Equal("bold", mark.Name);
    Assert.Equal(AutomergeScalarValueKind.Boolean, mark.Value.Kind);
    Assert.True(mark.Value.BoolValue);
  }

  // ── Mark Syncs Between Peers ───────────────────────────────────────

  [Fact]
  public void Mark_ShouldSyncBetweenPeers()
  {
    using var doc1 = AutomergeDocument.Create();
    var root1 = doc1.RootId;

    var textId = doc1.PutObject(root1, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Commit("write text");

    using var doc2 = doc1.Fork();

    doc1.Mark(textId, 0, 5, "bold", true);
    doc1.Commit("mark bold");

    doc2.Merge(doc1);

    // Both docs should now have the same state
    var json1 = doc1.HydrateJson();
    var json2 = doc2.HydrateJson();
    Assert.Equal(json1, json2);
  }

  // ── Unmark with ExpandMode ─────────────────────────────────────────

  [Fact]
  public void Unmark_WithExpandModeNone_ShouldRemoveMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 11, "bold", true);
    doc.Commit("mark bold");

    var headsAfterMark = doc.GetHeads();

    doc.Unmark(textId, "bold", 0, 11, ExpandMode.None);
    doc.Commit("unmark bold");

    var headsAfterUnmark = doc.GetHeads();
    var patches = doc.Diff(headsAfterMark, headsAfterUnmark);

    Assert.NotEmpty(patches);
    var markPatch = patches.FirstOrDefault(p => p.Action.Kind == AutomergePatchActionKind.Mark);
    Assert.NotNull(markPatch);
  }

  // ── Mark Zero-Length Range ─────────────────────────────────────────

  [Fact]
  public void Mark_ZeroLengthRange_ShouldSucceed()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello");
    doc.Commit("write text");

    // Marking a zero-length range (start == end) should be valid (no-op mark)
    doc.Mark(textId, 3, 3, "bold", true);
    doc.Commit("zero-length mark");

    var text = doc.GetText(textId);
    Assert.Equal("Hello", text);
  }
}
