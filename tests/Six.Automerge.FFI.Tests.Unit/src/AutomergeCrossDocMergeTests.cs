// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for cross-document merge scenarios with rich text:
/// two docs mark overlapping regions with different marks → merge → verify
/// both marks are preserved without conflict.
/// </summary>
[Trait("Category", "CrossDocMerge")]
public sealed class AutomergeCrossDocMergeTests
{
  // ── Overlapping Different Marks from Two Peers ─────────────────────

  [Fact]
  public void Merge_OverlappingDifferentMarks_ShouldPreserveBoth()
  {
    // ARRANGE: Create base document with shared text
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World from Automerge");
    doc1.Commit("shared text");

    // Fork to create second peer
    using var doc2 = doc1.Fork();

    // ACT: Doc1 marks "Hello World" (0-11) as bold
    doc1.Mark(textId, 0, 11, "bold", true);
    doc1.Commit("doc1: mark bold");

    // Doc2 marks "World from" (6-16) as italic (overlapping with bold)
    doc2.Mark(textId, 6, 16, "italic", true);
    doc2.Commit("doc2: mark italic");

    // Merge doc2 into doc1
    doc1.Merge(doc2);

    // ASSERT: Both marks should be preserved
    var marks = doc1.GetMarks(textId);
    Assert.Equal(2, marks.Count);

    var boldMark = marks.First(m => m.Name == "bold");
    Assert.Equal(0UL, boldMark.Start);
    Assert.Equal(11UL, boldMark.End);
    Assert.True(boldMark.Value.BoolValue);

    var italicMark = marks.First(m => m.Name == "italic");
    Assert.Equal(6UL, italicMark.Start);
    Assert.Equal(16UL, italicMark.End);
    Assert.True(italicMark.Value.BoolValue);

    // Verify text is unchanged
    Assert.Equal("Hello World from Automerge", doc1.GetText(textId));
  }

  // ── Same Mark Name, Different Values (Last Writer Wins) ────────────

  [Fact]
  public void Merge_SameMarkNameDifferentValues_ShouldResolveConflict()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Commit("shared text");

    using var doc2 = doc1.Fork();

    // Doc1 marks 0..5 with color=red
    doc1.Mark(textId, 0, 5, "color", "red");
    doc1.Commit("doc1: color red");

    // Doc2 marks 0..5 with color=blue (same range, same name, different value)
    doc2.Mark(textId, 0, 5, "color", "blue");
    doc2.Commit("doc2: color blue");

    // Merge
    doc1.Merge(doc2);

    // After merge, one value wins (LWW based on actor ordering)
    var marks = doc1.GetMarks(textId);
    var colorMarks = marks.Where(m => m.Name == "color").ToList();
    Assert.NotEmpty(colorMarks);

    // There should be exactly one color mark (the winner)
    // Automerge's LWW semantics means one value wins
    Assert.Single(colorMarks);
    var color = colorMarks[0].Value.StringValue;
    Assert.True(color == "red" || color == "blue",
      $"Expected 'red' or 'blue', got '{color}'");
  }

  // ── Three Marks from Two Peers, All Overlapping ────────────────────

  [Fact]
  public void Merge_ThreeOverlappingMarksFromTwoPeers_ShouldPreserveAll()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "The quick brown fox jumps");
    doc1.Commit("shared text");

    using var doc2 = doc1.Fork();

    // Doc1: bold on "quick" (4-9), link on "brown fox" (10-19)
    doc1.Mark(textId, 4, 9, "bold", true);
    doc1.Mark(textId, 10, 19, "link", "https://fox.example.com");
    doc1.Commit("doc1 marks");

    // Doc2: italic on "brown fox jumps" (10-25), overlapping with doc1's link
    doc2.Mark(textId, 10, 25, "italic", true);
    doc2.Commit("doc2 marks");

    // Merge
    doc1.Merge(doc2);

    var marks = doc1.GetMarks(textId);
    Assert.Equal(3, marks.Count);
    Assert.Contains(marks, m => m.Name == "bold");
    Assert.Contains(marks, m => m.Name == "italic");
    Assert.Contains(marks, m => m.Name == "link");

    // Verify each mark's range
    var bold = marks.First(m => m.Name == "bold");
    Assert.Equal(4UL, bold.Start);
    Assert.Equal(9UL, bold.End);

    var italic = marks.First(m => m.Name == "italic");
    Assert.Equal(10UL, italic.Start);
    Assert.Equal(25UL, italic.End);

    var link = marks.First(m => m.Name == "link");
    Assert.Equal(10UL, link.Start);
    Assert.Equal(19UL, link.End);
    Assert.Equal("https://fox.example.com", link.Value.StringValue);
  }

  // ── Merge with Concurrent Text Edits and Marks ─────────────────────

  [Fact]
  public void Merge_ConcurrentTextEditsAndMarks_ShouldPreserveBoth()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "ABCDEFGHIJ");
    doc1.Commit("shared text");

    using var doc2 = doc1.Fork();

    // Doc1: Mark "CDEF" (2-6) as bold, then insert "XX" at start
    doc1.Mark(textId, 2, 6, "bold", true);
    doc1.SpliceText(textId, 0, 0, "XX");
    doc1.Commit("doc1: mark + insert");

    // Doc2: Mark "EFGH" (4-8) as italic
    doc2.Mark(textId, 4, 8, "italic", true);
    doc2.Commit("doc2: mark italic");

    // Merge
    doc1.Merge(doc2);

    var text = doc1.GetText(textId);
    Assert.StartsWith("XX", text);
    Assert.Contains("ABCDEFGHIJ", text);

    var marks = doc1.GetMarks(textId);
    Assert.Contains(marks, m => m.Name == "bold");
    Assert.Contains(marks, m => m.Name == "italic");
  }

  // ── Bidirectional Merge: Both Docs Should Converge ─────────────────

  [Fact]
  public void Merge_Bidirectional_BothDocsShouldConverge()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Commit("shared text");

    using var doc2 = doc1.Fork();

    // Doc1: bold on "Hello"
    doc1.Mark(textId, 0, 5, "bold", true);
    doc1.Commit("doc1: bold");

    // Doc2: italic on "World"
    doc2.Mark(textId, 6, 11, "italic", true);
    doc2.Commit("doc2: italic");

    // Merge both directions
    doc1.Merge(doc2);
    doc2.Merge(doc1);

    // Both docs should have identical marks
    var marks1 = doc1.GetMarks(textId);
    var marks2 = doc2.GetMarks(textId);

    Assert.Equal(marks1.Count, marks2.Count);

    foreach(var m1 in marks1)
    {
      var m2 = marks2.FirstOrDefault(m => m.Name == m1.Name);
      Assert.NotNull(m2);
      Assert.Equal(m1.Start, m2.Start);
      Assert.Equal(m1.End, m2.End);
      Assert.Equal(m1.Value.Kind, m2.Value.Kind);
    }

    // Both docs should have identical text
    Assert.Equal(doc1.GetText(textId), doc2.GetText(textId));

    // Both docs should have identical spans
    var spans1 = doc1.GetSpans(textId);
    var spans2 = doc2.GetSpans(textId);
    Assert.Equal(spans1.Count, spans2.Count);
  }

  // ── Merge with Marks via Sync Protocol ─────────────────────────────

  [Fact]
  public void Merge_ViaSync_OverlappingMarks_ShouldConverge()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Commit("shared text");

    // Create a second doc by loading the same state
    var saved = doc1.Save();
    using var doc2 = AutomergeDocument.Load(saved);

    // Doc1: bold on range
    doc1.Mark(textId, 0, 5, "bold", true);
    doc1.Commit("doc1 bold");

    // Doc2: link on overlapping range
    doc2.Mark(textId, 3, 11, "link", "https://example.com");
    doc2.Commit("doc2 link");

    // Sync via protocol
    using var sync12 = doc1.CreateSyncState();
    using var sync21 = doc2.CreateSyncState();

    for(var i = 0; i < 10; i++)
    {
      var msg1 = doc1.GenerateSyncMessage(sync12);
      var msg2 = doc2.GenerateSyncMessage(sync21);
      if(msg1 is null && msg2 is null) break;
      if(msg1 is not null) doc2.ReceiveSyncMessage(sync21, msg1);
      if(msg2 is not null) doc1.ReceiveSyncMessage(sync12, msg2);
    }

    // Both docs should converge to same state
    Assert.Equal(doc1.GetText(textId), doc2.GetText(textId));

    var marks1 = doc1.GetMarks(textId);
    var marks2 = doc2.GetMarks(textId);
    Assert.Equal(marks1.Count, marks2.Count);

    Assert.Contains(marks1, m => m.Name == "bold");
    Assert.Contains(marks1, m => m.Name == "link");
    Assert.Contains(marks2, m => m.Name == "bold");
    Assert.Contains(marks2, m => m.Name == "link");
  }
}
