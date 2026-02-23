// Copyright (c) Six. Licensed under the MIT License.

using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

/// <summary>
/// Options for saving an Automerge document.
/// </summary>
[PublicAPI]
public sealed class SaveOptions
{
  /// <summary>
  /// Whether to apply DEFLATE compression to the document data.
  /// Defaults to <c>true</c>.
  /// </summary>
  public bool Deflate { get; init; } = true;

  /// <summary>
  /// Whether to retain orphan changes (changes whose dependencies are missing).
  /// Defaults to <c>true</c>.
  /// </summary>
  public bool RetainOrphans { get; init; } = true;

  internal Native.AutomergeSaveOptions ToNative() => new()
  {
    deflate = Deflate,
    retain_orphans = RetainOrphans
  };
}
