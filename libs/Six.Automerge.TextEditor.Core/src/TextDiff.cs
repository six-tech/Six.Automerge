// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.TextEdit;

/// <summary>
/// Computes minimal splice operations between two text strings.
/// Uses a prefix/suffix matching algorithm optimised for single-point edits
/// typical of text editor input.
/// </summary>
[PublicAPI]
public static class TextDiff
{
  /// <summary>
  /// Compares <paramref name="oldText"/> and <paramref name="newText"/>,
  /// returning a single splice that transforms old into new.
  /// </summary>
  public static SpliceResult ComputeSplice(string oldText, string newText)
  {
    ArgumentNullException.ThrowIfNull(oldText);
    ArgumentNullException.ThrowIfNull(newText);

    int prefixLen = 0;
    int minLen = Math.Min(oldText.Length, newText.Length);

    while(prefixLen < minLen && oldText[prefixLen] == newText[prefixLen])
    {
      prefixLen++;
    }

    int suffixLen = 0;
    int maxSuffix = minLen - prefixLen;

    while(suffixLen < maxSuffix
      && oldText[oldText.Length - 1 - suffixLen] == newText[newText.Length - 1 - suffixLen])
    {
      suffixLen++;
    }

    int deleteCount = oldText.Length - prefixLen - suffixLen;
    string insertText = newText.Substring(prefixLen, newText.Length - prefixLen - suffixLen);

    return new SpliceResult(prefixLen, deleteCount, insertText);
  }
}

/// <summary>
/// The result of a text diff splice computation.
/// </summary>
[PublicAPI]
public readonly record struct SpliceResult(int Offset, int DeleteCount, string InsertText)
{
  /// <summary>
  /// Gets a value indicating whether this splice represents an actual change.
  /// </summary>
  public bool HasChanges => DeleteCount > 0 || InsertText.Length > 0;

  public override string ToString() =>
    HasChanges
      ? $"Splice(offset={Offset}, delete={DeleteCount}, insert=\"{InsertText}\")"
      : "Splice(no change)";
}
