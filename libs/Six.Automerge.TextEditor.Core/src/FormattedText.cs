// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.TextEdit;

/// <summary>
/// A span of text with its formatting marks.
/// </summary>
[PublicAPI]
public sealed class FormattedSpan
{
  public string Text { get; }
  public int Offset { get; }
  public int Length { get; }
  public IReadOnlyDictionary<string, object?> Marks { get; }

  public FormattedSpan(string text, int offset, int length, IReadOnlyDictionary<string, object?> marks)
  {
    Text = text;
    Offset = offset;
    Length = length;
    Marks = marks;
  }
}

/// <summary>
/// Represents a fully formatted text document as a sequence of formatted spans.
/// </summary>
[PublicAPI]
public sealed class FormattedText
{
  public IReadOnlyList<FormattedSpan> Spans { get; }
  public string PlainText { get; }

  public FormattedText(IReadOnlyList<FormattedSpan> spans)
  {
    Spans = spans;
    PlainText = string.Concat(spans.Select(s => s.Text));
  }

  /// <summary>
  /// Creates a <see cref="FormattedText"/> from Automerge spans.
  /// Block spans are skipped; only text spans are converted.
  /// </summary>
  public static FormattedText FromSpans(IReadOnlyList<AutomergeSpan> automergeSpans)
  {
    ArgumentNullException.ThrowIfNull(automergeSpans);

    var spans = new List<FormattedSpan>();
    int offset = 0;

    for(int i = 0; i < automergeSpans.Count; i++)
    {
      if(automergeSpans[i] is not AutomergeTextSpan textSpan)
      {
        continue;
      }

      var marks = ConvertMarks(textSpan.Marks);
      int length = textSpan.Text.Length;
      spans.Add(new FormattedSpan(textSpan.Text, offset, length, marks));
      offset += length;
    }

    return new FormattedText(spans);
  }

  static IReadOnlyDictionary<string, object?> ConvertMarks(IReadOnlyList<AutomergeMarkInfo> markInfos)
  {
    if(markInfos.Count == 0)
    {
      return new Dictionary<string, object?>();
    }

    var marks = new Dictionary<string, object?>(markInfos.Count);
    for(int i = 0; i < markInfos.Count; i++)
    {
      var info = markInfos[i];
      marks[info.Name] = ExtractValue(info.Value);
    }

    return marks;
  }

  static object? ExtractValue(AutomergeScalarValue scalar) => scalar.Kind switch
  {
    AutomergeScalarValueKind.Boolean => scalar.BoolValue,
    AutomergeScalarValueKind.String => scalar.StringValue,
    AutomergeScalarValueKind.Int => scalar.IntValue,
    AutomergeScalarValueKind.Uint => scalar.UIntValue,
    AutomergeScalarValueKind.Double => scalar.DoubleValue,
    AutomergeScalarValueKind.Counter => scalar.CounterValue,
    AutomergeScalarValueKind.Timestamp => scalar.TimestampValue,
    AutomergeScalarValueKind.Null => null,
    _ => scalar.StringValue ?? (object?)scalar.IntValue
  };
}
