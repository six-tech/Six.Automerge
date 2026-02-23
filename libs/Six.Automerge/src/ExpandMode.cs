// Copyright (c) Six. Licensed under the MIT License.

using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

/// <summary>
/// Controls how a mark expands when text is inserted at its boundaries.
/// </summary>
/// <remarks>
/// See <see href="https://www.inkandswitch.com/peritext/">Peritext</see> for details
/// on which mode to use for different rich text formatting operations.
/// </remarks>
[PublicAPI]
public enum ExpandMode
{
  /// <summary>
  /// The mark expands when text is inserted before the start of the marked range.
  /// </summary>
  Before,

  /// <summary>
  /// The mark expands when text is inserted after the end of the marked range.
  /// This is the default behavior.
  /// </summary>
  After,

  /// <summary>
  /// The mark expands when text is inserted at either boundary of the marked range.
  /// </summary>
  Both,

  /// <summary>
  /// The mark does not expand when text is inserted at either boundary.
  /// </summary>
  None
}

internal static class ExpandModeExtensions
{
  public static Native.AutomergeExpandMark ToNative(this ExpandMode mode) => mode switch
  {
    ExpandMode.Before => Native.AutomergeExpandMark.Before,
    ExpandMode.After => Native.AutomergeExpandMark.After,
    ExpandMode.Both => Native.AutomergeExpandMark.Both,
    ExpandMode.None => Native.AutomergeExpandMark.None,
    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported expand mode.")
  };
}
