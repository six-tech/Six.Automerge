// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// End-to-end integration tests for rich text: create doc → insert text → add block
/// markers → apply marks (bold, italic, links) → splice within marked regions → sync
/// to second doc → verify spans match exactly.
/// </summary>
[Trait("Category", "RichTextIntegration")]
public sealed class AutomergeRichTextIntegrationTests
{
  // ── Full Pipeline: Text + Blocks + Marks + Splice + Sync ──────────

  [Fact]
  public void RichText_FullPipeline_TextBlocksMarksAndSync()
  {
    // ARRANGE: Create document with text
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World. This is paragraph one.");
    doc.Commit("insert text");

    // ACT: Add a block marker to split into paragraphs
    var blockId = doc.SplitBlock(textId, 13); // Split at "This is..."
    doc.Commit("split block");

    // Apply bold to "Hello" (chars 0-5)
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    // Apply italic to "World" (chars 6-11)
    doc.Mark(textId, 6, 11, "italic", true);
    doc.Commit("mark italic");

    // Apply a link mark to "paragraph one" (chars 22-35 in the original text,
    // but after block split the block marker takes up 1 char at index 13,
    // so "paragraph one" starts later)
    // Let's read the text to find the correct positions
    var text = doc.GetText(textId);
    var paraOneStart = text.IndexOf("paragraph one");
    if(paraOneStart >= 0)
    {
      doc.Mark(textId, (ulong)paraOneStart, (ulong)(paraOneStart + 13), "link", "https://example.com");
      doc.Commit("mark link");
    }

    // Splice text within the bold region: insert "Beautiful " before "World"
    doc.SpliceText(textId, 6, 0, "Beautiful ");
    doc.Commit("splice within marked region");

    // ASSERT: Verify marks survive splice
    var marks = doc.GetMarks(textId);
    Assert.NotEmpty(marks);

    var boldMark = marks.FirstOrDefault(m => m.Name == "bold");
    Assert.NotNull(boldMark);
    Assert.Equal(0UL, boldMark.Start);
    // Bold should still cover "Hello" (0..5)
    Assert.Equal(5UL, boldMark.End);

    var italicMark = marks.FirstOrDefault(m => m.Name == "italic");
    Assert.NotNull(italicMark);
    // Italic should have shifted right by 10 chars ("Beautiful ")
    // Original was 6..11, now 16..21
    Assert.Equal(16UL, italicMark.Start);
    Assert.Equal(21UL, italicMark.End);

    // Verify text content
    var finalText = doc.GetText(textId);
    Assert.Contains("Hello", finalText);
    Assert.Contains("Beautiful", finalText);
    Assert.Contains("World", finalText);

    // SYNC: Sync to a second document
    using var doc2 = AutomergeDocument.Create();
    using var syncAB = doc.CreateSyncState();
    using var syncBA = doc2.CreateSyncState();

    for(var i = 0; i < 10; i++)
    {
      var msgA = doc.GenerateSyncMessage(syncAB);
      var msgB = doc2.GenerateSyncMessage(syncBA);
      if(msgA is null && msgB is null) break;
      if(msgA is not null) doc2.ReceiveSyncMessage(syncBA, msgA);
      if(msgB is not null) doc.ReceiveSyncMessage(syncAB, msgB);
    }

    // Verify the synced document has identical text
    var text2 = doc2.GetText(textId);
    Assert.Equal(finalText, text2);

    // Verify marks match on synced document
    var marks2 = doc2.GetMarks(textId);
    Assert.Equal(marks.Count, marks2.Count);

    foreach(var originalMark in marks)
    {
      var syncedMark = marks2.FirstOrDefault(m => m.Name == originalMark.Name);
      Assert.NotNull(syncedMark);
      Assert.Equal(originalMark.Start, syncedMark.Start);
      Assert.Equal(originalMark.End, syncedMark.End);
      Assert.Equal(originalMark.Value.Kind, syncedMark.Value.Kind);
    }

    // Verify spans match exactly between the two documents
    var spans1 = doc.GetSpans(textId);
    var spans2 = doc2.GetSpans(textId);
    Assert.Equal(spans1.Count, spans2.Count);

    for(var i = 0; i < spans1.Count; i++)
    {
      Assert.Equal(spans1[i].GetType(), spans2[i].GetType());
      if(spans1[i] is AutomergeTextSpan ts1 && spans2[i] is AutomergeTextSpan ts2)
      {
        Assert.Equal(ts1.Text, ts2.Text);
        Assert.Equal(ts1.Marks.Count, ts2.Marks.Count);
      }
    }
  }

  // ── Text + Marks + Sync via Merge ──────────────────────────────────

  [Fact]
  public void RichText_MarksAndSync_ViaMerge()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Commit("write text");

    // Apply bold to "Hello"
    doc1.Mark(textId, 0, 5, "bold", true);
    // Apply italic to "World"
    doc1.Mark(textId, 6, 11, "italic", true);
    // Apply link to entire text
    doc1.Mark(textId, 0, 11, "link", "https://example.com");
    doc1.Commit("apply marks");

    // Fork and merge
    using var doc2 = doc1.Fork();

    // Verify text matches
    Assert.Equal("Hello World", doc2.GetText(textId));

    // Verify all 3 marks are present in the forked doc
    var marks2 = doc2.GetMarks(textId);
    Assert.Equal(3, marks2.Count);
    Assert.Contains(marks2, m => m.Name == "bold");
    Assert.Contains(marks2, m => m.Name == "italic");
    Assert.Contains(marks2, m => m.Name == "link");

    // Verify spans structure
    var spans = doc2.GetSpans(textId);
    Assert.NotEmpty(spans);

    // Should have at least text spans (the exact count depends on how marks split spans)
    var textSpans = spans.OfType<AutomergeTextSpan>().ToList();
    Assert.NotEmpty(textSpans);

    // All text content should be present when concatenated
    var allText = string.Join("", textSpans.Select(s => s.Text));
    Assert.Equal("Hello World", allText);
  }

  // ── Marks Survive Splice Then Sync ─────────────────────────────────

  [Fact]
  public void RichText_MarksSurviveSpliceThenSync()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "ABCDEFGHIJ");
    doc1.Mark(textId, 2, 8, "bold", true); // bold on "CDEFGH"
    doc1.Commit("text with bold");

    // Splice within the marked region: insert "XX" at position 5
    doc1.SpliceText(textId, 5, 0, "XX");
    doc1.Commit("splice in bold region");

    // Verify text is now "ABCDEXXFGHIJ"
    var text1 = doc1.GetText(textId);
    Assert.Equal("ABCDEXXFGHIJ", text1);

    // The bold mark should have expanded to include the inserted text
    var marks1 = doc1.GetMarks(textId);
    var bold1 = marks1.First(m => m.Name == "bold");
    Assert.Equal(2UL, bold1.Start);
    // End should have shifted by 2 for the insertion
    Assert.Equal(10UL, bold1.End);

    // Sync to second doc
    using var doc2 = doc1.Fork();
    var marks2 = doc2.GetMarks(textId);
    var bold2 = marks2.First(m => m.Name == "bold");

    Assert.Equal(bold1.Start, bold2.Start);
    Assert.Equal(bold1.End, bold2.End);
    Assert.Equal(doc1.GetText(textId), doc2.GetText(textId));
  }

  // ── Block Marker Preserves Across Sync ─────────────────────────────

  [Fact]
  public void RichText_BlockMarkerPreservesAcrossSync()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "First paragraph.Second paragraph.");
    doc.Commit("write text");

    // Split into two paragraphs at position 16
    doc.SplitBlock(textId, 16);
    doc.Commit("split block");

    // Verify spans have block markers
    var spans = doc.GetSpans(textId);
    Assert.True(spans.Count >= 2, "Should have at least 2 spans after split");

    // Sync to second doc
    using var doc2 = doc.Fork();
    var spans2 = doc2.GetSpans(textId);
    Assert.Equal(spans.Count, spans2.Count);

    // Verify block span types match
    for(var i = 0; i < spans.Count; i++)
    {
      Assert.Equal(spans[i].GetType(), spans2[i].GetType());
      if(spans[i] is AutomergeBlockSpan bs1 && spans2[i] is AutomergeBlockSpan bs2)
      {
        Assert.Equal(bs1.BlockType, bs2.BlockType);
      }
    }
  }

  // ── Multiple Marks on Same Range ───────────────────────────────────

  [Fact]
  public void RichText_MultipleMarksOnSameRange_SyncCorrectly()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Bold Italic Link");
    doc.Commit("write text");

    // Apply all three marks to the same range
    doc.Mark(textId, 0, 16, "bold", true);
    doc.Mark(textId, 0, 16, "italic", true);
    doc.Mark(textId, 0, 16, "link", "https://example.com");
    doc.Commit("triple mark");

    // Sync via save/load
    var saved = doc.Save();
    using var loaded = AutomergeDocument.Load(saved);

    // Verify all marks are preserved
    var marks = loaded.GetMarks(textId);
    Assert.Equal(3, marks.Count);
    Assert.Contains(marks, m => m.Name == "bold" && m.Value.BoolValue == true);
    Assert.Contains(marks, m => m.Name == "italic" && m.Value.BoolValue == true);
    Assert.Contains(marks, m => m.Name == "link" && m.Value.StringValue == "https://example.com");

    // Verify text
    Assert.Equal("Bold Italic Link", loaded.GetText(textId));
  }
}
