// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.Tests;

/// <summary>
/// Tests for marks read (GetMarks, GetMarksAtHeads) and spans read (GetSpans, GetSpansAtHeads) APIs.
/// </summary>
[Trait("Category", "MarksRead")]
public sealed class AutomergeMarksReadTests
{
  // ── GetMarks: Single Mark ───────────────────────────────────────

  [Fact]
  public void GetMarks_SingleBoldMark_ShouldReturnOneMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var marks = doc.GetMarks(textId);

    Assert.Single(marks);
    Assert.Equal("bold", marks[0].Name);
    Assert.Equal(0UL, marks[0].Start);
    Assert.Equal(5UL, marks[0].End);
    Assert.Equal(AutomergeScalarValueKind.Boolean, marks[0].Value.Kind);
    Assert.True(marks[0].Value.BoolValue);
  }

  // ── GetMarks: Multiple Marks ────────────────────────────────────

  [Fact]
  public void GetMarks_MultipleMarks_ShouldReturnAllMarks()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Mark(textId, 6, 11, "italic", true);
    doc.Commit("mark bold and italic");

    var marks = doc.GetMarks(textId);

    Assert.Equal(2, marks.Count);

    var boldMark = marks.FirstOrDefault(m => m.Name == "bold");
    Assert.NotNull(boldMark);
    Assert.Equal(0UL, boldMark.Start);
    Assert.Equal(5UL, boldMark.End);
    Assert.True(boldMark.Value.BoolValue);

    var italicMark = marks.FirstOrDefault(m => m.Name == "italic");
    Assert.NotNull(italicMark);
    Assert.Equal(6UL, italicMark.Start);
    Assert.Equal(11UL, italicMark.End);
    Assert.True(italicMark.Value.BoolValue);
  }

  [Fact]
  public void GetMarks_OverlappingDifferentNames_ShouldReturnBoth()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 8, "bold", true);
    doc.Mark(textId, 3, 11, "link", "https://example.com");
    doc.Commit("overlapping marks");

    var marks = doc.GetMarks(textId);

    Assert.Equal(2, marks.Count);
    Assert.Contains(marks, m => m.Name == "bold");
    Assert.Contains(marks, m => m.Name == "link");

    var linkMark = marks.First(m => m.Name == "link");
    Assert.Equal(AutomergeScalarValueKind.String, linkMark.Value.Kind);
    Assert.Equal("https://example.com", linkMark.Value.StringValue);
  }

  [Fact]
  public void GetMarks_StringValue_ShouldReturnStringMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Click here");
    doc.Mark(textId, 0, 10, "link", "https://automerge.org");
    doc.Commit("mark link");

    var marks = doc.GetMarks(textId);

    Assert.Single(marks);
    Assert.Equal("link", marks[0].Name);
    Assert.Equal(AutomergeScalarValueKind.String, marks[0].Value.Kind);
    Assert.Equal("https://automerge.org", marks[0].Value.StringValue);
  }

  [Fact]
  public void GetMarks_IntValue_ShouldReturnIntMark()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Large text");
    doc.Mark(textId, 0, 10, "fontSize", 24L);
    doc.Commit("mark fontSize");

    var marks = doc.GetMarks(textId);

    Assert.Single(marks);
    Assert.Equal("fontSize", marks[0].Name);
    Assert.Equal(AutomergeScalarValueKind.Int, marks[0].Value.Kind);
    Assert.Equal(24L, marks[0].Value.IntValue);
  }

  [Fact]
  public void GetMarks_NoMarks_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Plain text");
    doc.Commit("write text");

    var marks = doc.GetMarks(textId);

    Assert.Empty(marks);
  }

  [Fact]
  public void GetMarks_AfterUnmark_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    doc.Unmark(textId, "bold", 0, 5);
    doc.Commit("unmark bold");

    var marks = doc.GetMarks(textId);

    Assert.Empty(marks);
  }

  // ── GetMarksAtHeads: Historical ─────────────────────────────────

  [Fact]
  public void GetMarksAtHeads_ShouldReturnHistoricalMarks()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var headsWithBold = doc.GetHeads();

    doc.Mark(textId, 6, 11, "italic", true);
    doc.Commit("mark italic");

    // Current should have both marks
    var currentMarks = doc.GetMarks(textId);
    Assert.Equal(2, currentMarks.Count);

    // Historical should have only bold
    var historicalMarks = doc.GetMarksAtHeads(textId, headsWithBold);
    Assert.Single(historicalMarks);
    Assert.Equal("bold", historicalMarks[0].Name);
  }

  [Fact]
  public void GetMarksAtHeads_BeforeAnyMarks_ShouldReturnEmpty()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("write text");

    var headsNoMarks = doc.GetHeads();

    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var historicalMarks = doc.GetMarksAtHeads(textId, headsNoMarks);
    Assert.Empty(historicalMarks);
  }

  [Fact]
  public void GetMarksAtHeads_AfterUnmark_ShouldShowPreviousState()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var headsWithBold = doc.GetHeads();

    doc.Unmark(textId, "bold", 0, 5);
    doc.Commit("unmark bold");

    // Current should be empty
    var currentMarks = doc.GetMarks(textId);
    Assert.Empty(currentMarks);

    // Historical should still have bold
    var historicalMarks = doc.GetMarksAtHeads(textId, headsWithBold);
    Assert.Single(historicalMarks);
    Assert.Equal("bold", historicalMarks[0].Name);
  }

  // ── GetSpans: Text Only ─────────────────────────────────────────

  [Fact]
  public void GetSpans_PlainText_ShouldReturnSingleTextSpan()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("write text");

    var spans = doc.GetSpans(textId);

    Assert.Single(spans);
    var textSpan = Assert.IsType<AutomergeTextSpan>(spans[0]);
    Assert.Equal("Hello World", textSpan.Text);
    Assert.Empty(textSpan.Marks);
  }

  [Fact]
  public void GetSpans_EmptyText_ShouldReturnEmptyList()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.Commit("empty text");

    var spans = doc.GetSpans(textId);

    // Empty text may return empty or a single empty text span
    if(spans.Count > 0)
    {
      var textSpan = Assert.IsType<AutomergeTextSpan>(spans[0]);
      Assert.Equal("", textSpan.Text);
    }
  }

  // ── GetSpans: Text with Marks ───────────────────────────────────

  [Fact]
  public void GetSpans_MarkedText_ShouldSplitByMarks()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold on Hello");

    var spans = doc.GetSpans(textId);

    // Should split into at least 2 spans: bold "Hello" and plain " World"
    Assert.True(spans.Count >= 2, $"Expected at least 2 spans, got {spans.Count}");

    // FindAsync the bold span
    var boldSpan = spans
      .OfType<AutomergeTextSpan>()
      .FirstOrDefault(s => s.Marks.Any(m => m.Name == "bold"));
    Assert.NotNull(boldSpan);
    Assert.Equal("Hello", boldSpan.Text);
    Assert.Contains(boldSpan.Marks, m => m.Name == "bold" && m.Value.BoolValue == true);

    // FindAsync the plain span
    var plainSpan = spans
      .OfType<AutomergeTextSpan>()
      .FirstOrDefault(s => s.Text == " World");
    Assert.NotNull(plainSpan);
    Assert.Empty(plainSpan.Marks);
  }

  [Fact]
  public void GetSpans_MultipleMarksOnDifferentRegions_ShouldSplitCorrectly()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Bold and Italic");
    doc.Mark(textId, 0, 4, "bold", true);
    doc.Mark(textId, 9, 15, "italic", true);
    doc.Commit("mark regions");

    var spans = doc.GetSpans(textId);

    var textSpans = spans.OfType<AutomergeTextSpan>().ToList();

    // Should have at least 3 spans: bold "Bold", plain " and ", italic "Italic"
    Assert.True(textSpans.Count >= 3, $"Expected at least 3 text spans, got {textSpans.Count}");

    // Verify "Bold" has bold mark
    var boldSpan = textSpans.FirstOrDefault(s => s.Text == "Bold");
    Assert.NotNull(boldSpan);
    Assert.Contains(boldSpan.Marks, m => m.Name == "bold");

    // Verify "Italic" has italic mark
    var italicSpan = textSpans.FirstOrDefault(s => s.Text == "Italic");
    Assert.NotNull(italicSpan);
    Assert.Contains(italicSpan.Marks, m => m.Name == "italic");
  }

  [Fact]
  public void GetSpans_OverlappingMarks_ShouldShowBothMarksOnOverlap()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "ABCDEF");
    doc.Mark(textId, 0, 4, "bold", true);     // ABCD bold
    doc.Mark(textId, 2, 6, "italic", true);   // CDEF italic
    doc.Commit("overlapping marks");

    var spans = doc.GetSpans(textId);

    var textSpans = spans.OfType<AutomergeTextSpan>().ToList();

    // FindAsync the overlap region (CD) which should have both marks
    var overlapSpan = textSpans.FirstOrDefault(s => s.Marks.Count == 2);
    Assert.NotNull(overlapSpan);
    Assert.Contains(overlapSpan.Marks, m => m.Name == "bold");
    Assert.Contains(overlapSpan.Marks, m => m.Name == "italic");
  }

  // ── GetSpansAtHeads: Historical ─────────────────────────────────

  [Fact]
  public void GetSpansAtHeads_ShouldReturnHistoricalSpans()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    var headsWithBold = doc.GetHeads();

    doc.Mark(textId, 6, 11, "italic", true);
    doc.Commit("mark italic");

    // Historical spans should only show bold
    var historicalSpans = doc.GetSpansAtHeads(textId, headsWithBold);

    var textSpans = historicalSpans.OfType<AutomergeTextSpan>().ToList();
    var allMarks = textSpans.SelectMany(s => s.Marks).ToList();

    Assert.Contains(allMarks, m => m.Name == "bold");
    Assert.DoesNotContain(allMarks, m => m.Name == "italic");
  }

  [Fact]
  public void GetSpansAtHeads_PlainTextAtHistoricalPoint_ShouldHaveNoMarks()
  {
    using var doc = AutomergeDocument.Create();
    var root = doc.RootId;

    var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
    doc.SpliceText(textId, 0, 0, "Hello World");
    doc.Commit("write text");

    var headsPlain = doc.GetHeads();

    doc.Mark(textId, 0, 5, "bold", true);
    doc.Commit("mark bold");

    // Historical spans should have no marks
    var historicalSpans = doc.GetSpansAtHeads(textId, headsPlain);

    var textSpans = historicalSpans.OfType<AutomergeTextSpan>().ToList();
    Assert.NotEmpty(textSpans);

    var allMarks = textSpans.SelectMany(s => s.Marks).ToList();
    Assert.Empty(allMarks);
  }

  // ── Marks Survive Sync ──────────────────────────────────────────

  [Fact]
  public void GetMarks_ShouldSurviveSyncBetweenDocuments()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Mark(textId, 0, 5, "bold", true);
    doc1.Mark(textId, 6, 11, "link", "https://example.com");
    doc1.Commit("write and mark");

    // Fork and sync
    using var doc2 = doc1.Fork();

    // Both docs should see same marks
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
  public void GetMarks_ShouldSurviveSyncViaMessages()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Mark(textId, 0, 5, "bold", true);
    doc1.Mark(textId, 6, 11, "link", "https://example.com");
    doc1.Commit("write and mark");

    // Create doc2 from scratch and sync via messages
    using var doc2 = AutomergeDocument.Create();
    using var sync1 = doc1.CreateSyncState();
    using var sync2 = doc2.CreateSyncState();

    for(var i = 0; i < 10; i++)
    {
      var msg1 = doc1.GenerateSyncMessage(sync1);
      var msg2 = doc2.GenerateSyncMessage(sync2);
      if(msg1 == null && msg2 == null)
        break;
      if(msg1 != null)
        doc2.ReceiveSyncMessage(sync2, msg1);
      if(msg2 != null)
        doc1.ReceiveSyncMessage(sync1, msg2);
    }

    // Both docs should see same marks
    var marks2 = doc2.GetMarks(textId);
    Assert.Equal(2, marks2.Count);

    var boldMark = marks2.FirstOrDefault(m => m.Name == "bold");
    Assert.NotNull(boldMark);
    Assert.Equal(0UL, boldMark.Start);
    Assert.Equal(5UL, boldMark.End);
    Assert.Equal(AutomergeScalarValueKind.Boolean, boldMark.Value.Kind);
    Assert.True(boldMark.Value.BoolValue);

    var linkMark = marks2.FirstOrDefault(m => m.Name == "link");
    Assert.NotNull(linkMark);
    Assert.Equal(6UL, linkMark.Start);
    Assert.Equal(11UL, linkMark.End);
    Assert.Equal(AutomergeScalarValueKind.String, linkMark.Value.Kind);
    Assert.Equal("https://example.com", linkMark.Value.StringValue);
  }

  [Fact]
  public void GetSpans_ShouldSurviveSyncBetweenDocuments()
  {
    using var doc1 = AutomergeDocument.Create();
    var root = doc1.RootId;

    var textId = doc1.PutObject(root, "content", AutomergeContainerType.Text);
    doc1.SpliceText(textId, 0, 0, "Hello World");
    doc1.Mark(textId, 0, 5, "bold", true);
    doc1.Commit("write and mark");

    using var doc2 = doc1.Fork();

    var spans1 = doc1.GetSpans(textId);
    var spans2 = doc2.GetSpans(textId);

    // Both should have same span structure
    Assert.Equal(spans1.Count, spans2.Count);

    for(var i = 0; i < spans1.Count; i++)
    {
      Assert.Equal(spans1[i].GetType(), spans2[i].GetType());
      if(spans1[i] is AutomergeTextSpan t1 && spans2[i] is AutomergeTextSpan t2)
      {
        Assert.Equal(t1.Text, t2.Text);
        Assert.Equal(t1.Marks.Count, t2.Marks.Count);
      }
    }
  }

  // ── GetMarks: Save/Load Round-Trip ─────────────────────────────

  [Fact]
  public void GetMarks_ShouldSurviveSaveAndLoad()
  {
    byte[] saved;
    using(var doc = AutomergeDocument.Create())
    {
      var root = doc.RootId;
      var textId = doc.PutObject(root, "content", AutomergeContainerType.Text);
      doc.SpliceText(textId, 0, 0, "Hello World");
      doc.Mark(textId, 0, 5, "bold", true);
      doc.Mark(textId, 6, 11, "fontSize", 16L);
      doc.Commit("write and mark");
      saved = doc.Save();
    }

    using var loaded = AutomergeDocument.Load(saved);
    var loadedTextId = loaded.GetObject(loaded.RootId, "content");
    Assert.NotNull(loadedTextId);

    var marks = loaded.GetMarks(loadedTextId);
    Assert.Equal(2, marks.Count);

    var boldMark = marks.FirstOrDefault(m => m.Name == "bold");
    Assert.NotNull(boldMark);
    Assert.True(boldMark.Value.BoolValue);

    var fontMark = marks.FirstOrDefault(m => m.Name == "fontSize");
    Assert.NotNull(fontMark);
    Assert.Equal(16L, fontMark.Value.IntValue);
  }
}
