// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Integration test: full rich text workflow with blocks, marks, and sync.
/// </summary>
[Trait("Category", "BlocksIntegration")]
public sealed class AutomergeBlockIntegrationTests
{
  [Fact]
  public void FullRichTextWorkflow_CreateBlocksMarksSync_SpansIdenticalAfterSync()
  {
    // ── Step 1: Create document with rich text structure ──────────
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    // Create a text object
    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);

    // Write initial text
    doc1.SpliceText(textId, 0, 0, "IntroductionThis is the first paragraph.This is the second paragraph.");

    // ── Step 2: Add block markers ────────────────────────────────
    // Insert heading block at position 0 (before "Introduction")
    var headingBlock = doc1.SplitBlock(textId, 0);
    doc1.PutString(headingBlock, "type", "heading");

    // Insert paragraph block before "This is the first..."
    // After split at 0, "Introduction" is at offset 1-12, then text continues
    // The text after "Introduction" starts at index 13 (12 chars + 1 block marker)
    var para1Block = doc1.SplitBlock(textId, 13);
    doc1.PutString(para1Block, "type", "paragraph");

    // Insert paragraph block before "This is the second..."
    // After two splits, we need to account for block markers
    // "Introduction" (12) + block(1) + "This is the first paragraph." (29) + block(1) = index 43
    var para2Block = doc1.SplitBlock(textId, 43);
    doc1.PutString(para2Block, "type", "paragraph");

    // ── Step 3: Apply marks ──────────────────────────────────────
    // Bold "Introduction" (after the heading block marker at 0, text at 1..12)
    doc1.Mark(textId, 1, 13, "bold", true);

    // Italic "first" in "This is the first paragraph."
    // After heading block at 0, "Introduction" at 1-12, para1 block at 13, "This is the " at 14-25, "first" at 26-30
    doc1.Mark(textId, 26, 31, "italic", true);

    doc1.Commit("full rich text structure");

    // ── Step 4: Read spans from doc1 ─────────────────────────────
    var spans1 = doc1.GetSpans(textId);

    // Verify we have block spans
    var blockSpans1 = spans1.OfType<AutomergeBlockSpan>().ToList();
    Assert.True(blockSpans1.Count >= 3, $"Expected at least 3 block spans, got {blockSpans1.Count}");

    // Verify we have text spans with marks
    var textSpans1 = spans1.OfType<AutomergeTextSpan>().ToList();
    Assert.NotEmpty(textSpans1);

    // Verify marks exist
    var allMarks1 = textSpans1.SelectMany(s => s.Marks).ToList();
    Assert.Contains(allMarks1, m => m.Name == "bold");
    Assert.Contains(allMarks1, m => m.Name == "italic");

    // ── Step 5: Sync to doc2 via sync protocol ───────────────────
    using var doc2 = AutomergeDocument.Create();
    using var sync1to2 = doc1.CreateSyncState();
    using var sync2to1 = doc2.CreateSyncState();

    for(var i = 0; i < 10; i++)
    {
      var msg1 = doc1.GenerateSyncMessage(sync1to2);
      var msg2 = doc2.GenerateSyncMessage(sync2to1);
      if(msg1 == null && msg2 == null)
        break;
      if(msg1 != null)
        doc2.ReceiveSyncMessage(sync2to1, msg1);
      if(msg2 != null)
        doc1.ReceiveSyncMessage(sync1to2, msg2);
    }

    // ── Step 6: Verify spans are identical after sync ────────────
    var spans2 = doc2.GetSpans(textId);

    Assert.Equal(spans1.Count, spans2.Count);

    for(var i = 0; i < spans1.Count; i++)
    {
      Assert.Equal(spans1[i].GetType(), spans2[i].GetType());

      if(spans1[i] is AutomergeTextSpan t1 && spans2[i] is AutomergeTextSpan t2)
      {
        Assert.Equal(t1.Text, t2.Text);
        Assert.Equal(t1.Marks.Count, t2.Marks.Count);

        for(var m = 0; m < t1.Marks.Count; m++)
        {
          Assert.Equal(t1.Marks[m].Name, t2.Marks[m].Name);
          Assert.Equal(t1.Marks[m].Value.Kind, t2.Marks[m].Value.Kind);
        }
      }

      if(spans1[i] is AutomergeBlockSpan b1 && spans2[i] is AutomergeBlockSpan b2)
      {
        Assert.Equal(b1.BlockType, b2.BlockType);
        Assert.Equal(b1.Parents.Count, b2.Parents.Count);

        for(var p = 0; p < b1.Parents.Count; p++)
        {
          Assert.Equal(b1.Parents[p], b2.Parents[p]);
        }
      }
    }

    // ── Step 7: Verify text content matches ──────────────────────
    var text1 = doc1.GetText(textId);
    var text2 = doc2.GetText(textId);
    Assert.Equal(text1, text2);

    // ── Step 8: Verify marks match ───────────────────────────────
    var marks1 = doc1.GetMarks(textId);
    var marks2 = doc2.GetMarks(textId);

    Assert.Equal(marks1.Count, marks2.Count);

    for(var i = 0; i < marks1.Count; i++)
    {
      Assert.Equal(marks1[i].Name, marks2[i].Name);
      Assert.Equal(marks1[i].Start, marks2[i].Start);
      Assert.Equal(marks1[i].End, marks2[i].End);
      Assert.Equal(marks1[i].Value.Kind, marks2[i].Value.Kind);
    }
  }

  [Fact]
  public void RichText_SaveLoadRoundTrip_PreservesBlocksAndMarks()
  {
    byte[] saved;
    using(var doc = AutomergeDocument.Create())
    {
      var root = doc.RootId;
      var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
      doc.SpliceText(textId, 0, 0, "HeadingParagraph text");

      var headingBlock = doc.SplitBlock(textId, 0);
      doc.PutString(headingBlock, "type", "heading");

      var paraBlock = doc.SplitBlock(textId, 8);
      doc.PutString(paraBlock, "type", "paragraph");

      doc.Mark(textId, 1, 8, "bold", true);
      doc.Commit("rich text structure");

      saved = doc.Save();
    }

    using var loaded = AutomergeDocument.Load(saved);
    var loadedTextId = loaded.GetObject(loaded.RootId, "content");
    Assert.NotNull(loadedTextId);

    var spans = loaded.GetSpans(loadedTextId);

    // Verify blocks survived
    var blockSpans = spans.OfType<AutomergeBlockSpan>().ToList();
    Assert.True(blockSpans.Count >= 2, $"Expected at least 2 blocks after load, got {blockSpans.Count}");

    Assert.Contains(blockSpans, b => b.BlockType == "heading");
    Assert.Contains(blockSpans, b => b.BlockType == "paragraph");

    // Verify marks survived
    var marks = loaded.GetMarks(loadedTextId);
    Assert.Contains(marks, m => m.Name == "bold");
  }

  [Fact]
  public void RichText_ConcurrentEdits_MergeBlocksAndMarks()
  {
    // Create base document with text
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;
    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "ABCDEF");
    doc1.Commit("initial");

    // Fork
    using var doc2 = doc1.Fork();

    // Doc1: Add block at position 3
    var block1 = doc1.SplitBlock(textId, 3);
    doc1.PutString(block1, "type", "paragraph");
    doc1.Mark(textId, 0, 3, "bold", true);
    doc1.Commit("doc1 changes");

    // Doc2: Add mark on different region
    doc2.Mark(textId, 3, 6, "italic", true);
    doc2.Commit("doc2 changes");

    // Merge
    doc1.Merge(doc2);
    doc2.Merge(doc1);

    // Both should converge
    var text1 = doc1.GetText(textId);
    var text2 = doc2.GetText(textId);
    Assert.Equal(text1, text2);

    // Both should have the block
    var spans1 = doc1.GetSpans(textId);
    var spans2 = doc2.GetSpans(textId);
    Assert.Contains(spans1, s => s is AutomergeBlockSpan);
    Assert.Contains(spans2, s => s is AutomergeBlockSpan);

    // Both should have both marks
    var marks1 = doc1.GetMarks(textId);
    var marks2 = doc2.GetMarks(textId);
    Assert.Contains(marks1, m => m.Name == "bold");
    Assert.Contains(marks1, m => m.Name == "italic");
    Assert.Equal(marks1.Count, marks2.Count);
  }
}
