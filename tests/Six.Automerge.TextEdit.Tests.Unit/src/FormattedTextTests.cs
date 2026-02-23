// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class FormattedTextTests
{
  [Fact]
  public void FormattedSpan_Properties_AreSetCorrectly()
  {
    var marks = new Dictionary<string, object?> { ["bold"] = true };
    var span = new FormattedSpan("hello", 0, 5, marks);

    span.Text.Should().Be("hello");
    span.Offset.Should().Be(0);
    span.Length.Should().Be(5);
    span.Marks.Should().ContainKey("bold").WhoseValue.Should().Be(true);
  }

  [Fact]
  public void FormattedText_PlainText_ConcatenatesAllSpans()
  {
    var spans = new List<FormattedSpan>
    {
      new("hello", 0, 5, new Dictionary<string, object?>()),
      new(" ", 5, 1, new Dictionary<string, object?>()),
      new("world", 6, 5, new Dictionary<string, object?>())
    };

    var ft = new FormattedText(spans);

    ft.PlainText.Should().Be("hello world");
    ft.Spans.Should().HaveCount(3);
  }

  [Fact]
  public void FormattedText_EmptySpans_ProducesEmptyPlainText()
  {
    var ft = new FormattedText([]);

    ft.PlainText.Should().BeEmpty();
    ft.Spans.Should().BeEmpty();
  }

  [Fact]
  public void FormattedText_FromSpans_ConvertsTextSpans()
  {
    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("hello", []),
      new AutomergeTextSpan(" bold", [CreateBoolMark("bold", true, 0, 5)])
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans.Should().HaveCount(2);
    result.PlainText.Should().Be("hello bold");

    result.Spans[0].Text.Should().Be("hello");
    result.Spans[0].Marks.Should().BeEmpty();

    result.Spans[1].Text.Should().Be(" bold");
    result.Spans[1].Marks.Should().ContainKey("bold").WhoseValue.Should().Be(true);
  }

  [Fact]
  public void FormattedText_FromSpans_ConvertsStringMarkValues()
  {
    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("link", [CreateStringMark("href", "http://example.com", 0, 4)])
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans[0].Marks.Should().ContainKey("href")
      .WhoseValue.Should().Be("http://example.com");
  }

  [Fact]
  public void FormattedText_FromSpans_ConvertsIntMarkValues()
  {
    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("text", [CreateIntMark("fontSize", 14, 0, 4)])
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans[0].Marks.Should().ContainKey("fontSize")
      .WhoseValue.Should().Be(14L);
  }

  [Fact]
  public void FormattedText_FromSpans_SkipsBlockSpans()
  {
    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("before", []),
      new AutomergeBlockSpan("paragraph", [], "{}"),
      new AutomergeTextSpan("after", [])
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans.Should().HaveCount(2);
    result.PlainText.Should().Be("beforeafter");
  }

  [Fact]
  public void FormattedText_FromSpans_NullInput_ThrowsArgumentNullException()
  {
    var act = () => FormattedText.FromSpans(null!);

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void FormattedText_FromSpans_TracksOffsets()
  {
    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("abc", []),
      new AutomergeTextSpan("de", [CreateBoolMark("italic", true, 0, 2)])
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans[0].Offset.Should().Be(0);
    result.Spans[0].Length.Should().Be(3);
    result.Spans[1].Offset.Should().Be(3);
    result.Spans[1].Length.Should().Be(2);
  }

  [Fact]
  public void FormattedText_FromSpans_MultipleMarksOnSameSpan()
  {
    var marks = new List<AutomergeMarkInfo>
    {
      CreateBoolMark("bold", true, 0, 5),
      CreateBoolMark("italic", true, 0, 5)
    };

    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("hello", marks)
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans[0].Marks.Should().HaveCount(2);
    result.Spans[0].Marks["bold"].Should().Be(true);
    result.Spans[0].Marks["italic"].Should().Be(true);
  }

  [Fact]
  public void FormattedText_FromSpans_OverlappingMarks_DifferentMarksOnAdjacentSpans()
  {
    // Simulates: "hello world" where bold spans all, italic spans "world" only
    // Automerge splits this into two spans: "hello " (bold) and "world" (bold+italic)
    var automergeSpans = new List<AutomergeSpan>
    {
      new AutomergeTextSpan("hello ", [CreateBoolMark("bold", true, 0, 6)]),
      new AutomergeTextSpan("world", [
        CreateBoolMark("bold", true, 0, 5),
        CreateBoolMark("italic", true, 0, 5)
      ])
    };

    var result = FormattedText.FromSpans(automergeSpans);

    result.Spans.Should().HaveCount(2);
    result.PlainText.Should().Be("hello world");

    // First span: bold only
    result.Spans[0].Marks.Should().ContainKey("bold");
    result.Spans[0].Marks.Should().NotContainKey("italic");

    // Second span: bold + italic
    result.Spans[1].Marks.Should().ContainKey("bold");
    result.Spans[1].Marks.Should().ContainKey("italic");
  }

  static AutomergeMarkInfo CreateBoolMark(string name, bool value, ulong start, ulong end) =>
    AutomergeMarkInfoFactory.CreateBool(name, value, start, end);

  static AutomergeMarkInfo CreateStringMark(string name, string value, ulong start, ulong end) =>
    AutomergeMarkInfoFactory.CreateString(name, value, start, end);

  static AutomergeMarkInfo CreateIntMark(string name, long value, ulong start, ulong end) =>
    AutomergeMarkInfoFactory.CreateInt(name, value, start, end);
}
