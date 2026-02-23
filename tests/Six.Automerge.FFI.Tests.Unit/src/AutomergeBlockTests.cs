// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for block markers (SplitBlock, JoinBlock) and UpdateSpans APIs.
/// </summary>
[Trait("Category", "Blocks")]
public sealed class AutomergeBlockTests
{
  // ── SplitBlock: Basic ─────────────────────────────────────────

  [Fact]
  public void SplitBlock_ShouldCreateBlockMarker()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    var blockId = doc.SplitBlock(textId, 0);
    doc.Commit("split block");

    Assert.NotNull(blockId);

    var spans = doc.GetSpans(textId);

    // Should have at least one block span
    Assert.Contains(spans, s => s is AutomergeBlockSpan);
  }

  [Fact]
  public void SplitBlock_ShouldReturnWritableObjId()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");

    var blockId = doc.SplitBlock(textId, 0);
    doc.PutString(blockId, "type", "paragraph");
    doc.Commit("split and type block");

    var spans = doc.GetSpans(textId);

    var blockSpan = spans.OfType<AutomergeBlockSpan>().FirstOrDefault();
    Assert.NotNull(blockSpan);
    Assert.Equal("paragraph", blockSpan.BlockType);
  }

  [Fact]
  public void SplitBlock_WithTypeAndParents_ShouldBeReadableViaSpans()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Item one");

    var blockId = doc.SplitBlock(textId, 0);
    doc.PutString(blockId, "type", "ordered-list-item");
    var parentsId = doc.PutObject(blockId, "parents", AutomergeContainerType.List);
    doc.ListInsertString(parentsId, 0, "ordered-list");
    doc.Commit("block with parents");

    var spans = doc.GetSpans(textId);

    var blockSpan = spans.OfType<AutomergeBlockSpan>().FirstOrDefault();
    Assert.NotNull(blockSpan);
    Assert.Equal("ordered-list-item", blockSpan.BlockType);
    Assert.Single(blockSpan.Parents);
    Assert.Equal("ordered-list", blockSpan.Parents[0]);
  }

  [Fact]
  public void SplitBlock_AtMiddle_ShouldSplitText()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "HelloWorld");

    var blockId = doc.SplitBlock(textId, 5);
    doc.PutString(blockId, "type", "paragraph");
    doc.Commit("split at middle");

    var spans = doc.GetSpans(textId);

    var textSpans = spans.OfType<AutomergeTextSpan>().ToList();
    var blockSpans = spans.OfType<AutomergeBlockSpan>().ToList();

    Assert.Single(blockSpans);
    // Should have text before and after the block
    Assert.True(textSpans.Count >= 2, $"Expected at least 2 text spans, got {textSpans.Count}");

    var combined = string.Join("", textSpans.Select(t => t.Text));
    Assert.Equal("HelloWorld", combined);
  }

  // ── JoinBlock: Remove Block ───────────────────────────────────

  [Fact]
  public void JoinBlock_ShouldRemoveBlockMarker()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "HelloWorld");

    var blockId = doc.SplitBlock(textId, 5);
    doc.PutString(blockId, "type", "paragraph");
    doc.Commit("split block");

    // Verify block exists
    var spansBefore = doc.GetSpans(textId);
    Assert.Contains(spansBefore, s => s is AutomergeBlockSpan);

    // Remove the block (at index 5 where it was inserted)
    doc.JoinBlock(textId, 5);
    doc.Commit("join block");

    var spansAfter = doc.GetSpans(textId);
    Assert.DoesNotContain(spansAfter, s => s is AutomergeBlockSpan);

    // Text should be rejoined
    var textSpans = spansAfter.OfType<AutomergeTextSpan>().ToList();
    var combined = string.Join("", textSpans.Select(t => t.Text));
    Assert.Equal("HelloWorld", combined);
  }

  // ── UpdateSpans: Minimal Diff ─────────────────────────────────

  [Fact]
  public void UpdateSpans_MinimalDiff_ShouldPreserveIdenticalContent()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("initial text");

    var spansBefore = doc.GetSpans(textId);

    // Update with identical content
    doc.UpdateSpans(textId, spansBefore);
    doc.Commit("update identical");

    var spansAfter = doc.GetSpans(textId);

    Assert.Equal(spansBefore.Count, spansAfter.Count);
    var textBefore = spansBefore.OfType<AutomergeTextSpan>().First();
    var textAfter = spansAfter.OfType<AutomergeTextSpan>().First();
    Assert.Equal(textBefore.Text, textAfter.Text);
  }

  // ── UpdateSpans: Text Changes ─────────────────────────────────

  [Fact]
  public void UpdateSpans_TextChange_ShouldModifyContent()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("initial text");

    // Change text via UpdateSpans
    var newSpans = new AutomergeSpan[]
    {
      new AutomergeTextSpan("Goodbye World", Array.Empty<AutomergeMarkInfo>())
    };

    doc.UpdateSpans(textId, newSpans);
    doc.Commit("update text");

    var text = doc.GetText(textId);
    Assert.Equal("Goodbye World", text);
  }

  // ── UpdateSpans: Block Insertion ──────────────────────────────

  [Fact]
  public void UpdateSpans_InsertBlock_ShouldAddBlockMarker()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "HelloWorld");
    doc.Commit("initial text");

    // Insert a block between Hello and World
    var newSpans = new AutomergeSpan[]
    {
      new AutomergeTextSpan("Hello", Array.Empty<AutomergeMarkInfo>()),
      new AutomergeBlockSpan("paragraph", Array.Empty<string>(), "{}"),
      new AutomergeTextSpan("World", Array.Empty<AutomergeMarkInfo>())
    };

    doc.UpdateSpans(textId, newSpans);
    doc.Commit("insert block");

    var spans = doc.GetSpans(textId);

    // Verify block was created
    Assert.Contains(spans, s => s is AutomergeBlockSpan);

    // Verify text content is preserved
    var textContent = string.Join("", spans.OfType<AutomergeTextSpan>().Select(t => t.Text));
    Assert.Equal("HelloWorld", textContent);
  }

  // ── UpdateSpans: Round-trip ───────────────────────────────────

  [Fact]
  public void UpdateSpans_RoundTrip_ReadModifyUpdateRead()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "First paragraph");

    // Add a block marker
    var blockId = doc.SplitBlock(textId, 0);
    doc.PutString(blockId, "type", "paragraph");
    doc.Commit("initial structure");

    // Read spans
    var originalSpans = doc.GetSpans(textId);

    // Modify: append new text span after block
    var modified = new List<AutomergeSpan>(originalSpans);
    modified.Add(new AutomergeBlockSpan("heading", Array.Empty<string>(), "{}"));
    modified.Add(new AutomergeTextSpan("Second heading", Array.Empty<AutomergeMarkInfo>()));

    // Update
    doc.UpdateSpans(textId, modified);
    doc.Commit("added heading");

    // Read again and verify
    var finalSpans = doc.GetSpans(textId);

    var blockSpans = finalSpans.OfType<AutomergeBlockSpan>().ToList();
    Assert.True(blockSpans.Count >= 2, $"Expected at least 2 blocks, got {blockSpans.Count}");

    // Verify all text content
    var allText = string.Join("", finalSpans.OfType<AutomergeTextSpan>().Select(t => t.Text));
    Assert.Contains("First paragraph", allText);
    Assert.Contains("Second heading", allText);
  }

  [Fact]
  public void UpdateSpans_RoundTrip_PreservesMarks()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Bold text and plain");
    doc.Mark(textId, 0, 9, "bold", true);
    doc.Commit("initial with marks");

    // Read spans (should have bold "Bold text" and plain " and plain")
    var spans = doc.GetSpans(textId);

    // Update with same spans — should be a no-op
    doc.UpdateSpans(textId, spans);
    doc.Commit("update identical with marks");

    // Verify marks survived
    var marks = doc.GetMarks(textId);
    var boldMark = marks.FirstOrDefault(m => m.Name == "bold");
    Assert.NotNull(boldMark);
    Assert.Equal(0UL, boldMark.Start);
    Assert.Equal(9UL, boldMark.End);
  }
}
