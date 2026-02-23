// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge;

/// <summary>
/// Represents a stable position reference in a sequence (list or text) that
/// tracks a logical position even as concurrent edits shift indices.
/// </summary>
[PublicAPI]
public sealed class AutomergeCursor
{
  byte[] _bytes;

  internal AutomergeCursor(byte[] bytes)
  {
    _bytes = bytes;
  }

  /// <summary>
  /// Creates a cursor from its serialized byte representation.
  /// </summary>
  public static AutomergeCursor FromBytes(ReadOnlySpan<byte> bytes) =>
    new(bytes.ToArray());

  /// <summary>
  /// Returns the serialized byte representation of this cursor.
  /// </summary>
  public byte[] ToBytes() => (byte[])_bytes.Clone();

  internal ReadOnlySpan<byte> AsSpan() => _bytes;
}
