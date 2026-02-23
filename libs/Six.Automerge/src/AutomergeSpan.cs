// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge;

/// <summary>
/// Base type for rich text spans returned by <see cref="AutomergeDocument.GetSpans"/>.
/// A span is either a <see cref="AutomergeTextSpan"/> (text with marks) or
/// an <see cref="AutomergeBlockSpan"/> (block-level marker).
/// </summary>
[PublicAPI]
public abstract class AutomergeSpan;

/// <summary>
/// A span of text with its associated marks (formatting annotations).
/// </summary>
[PublicAPI]
public sealed class AutomergeTextSpan : AutomergeSpan
{
  public string Text { get; }
  public IReadOnlyList<AutomergeMarkInfo> Marks { get; }

  public AutomergeTextSpan(string text, IReadOnlyList<AutomergeMarkInfo> marks)
  {
    Text = text;
    Marks = marks;
  }
}

/// <summary>
/// A block-level marker that separates paragraphs, headings, or other structural elements.
/// </summary>
[PublicAPI]
public sealed class AutomergeBlockSpan : AutomergeSpan
{
  public string BlockType { get; }
  public IReadOnlyList<string> Parents { get; }
  public string AttributesJson { get; }

  public AutomergeBlockSpan(
    string blockType,
    IReadOnlyList<string> parents,
    string attributesJson)
  {
    BlockType = blockType;
    Parents = parents;
    AttributesJson = attributesJson;
  }
}
