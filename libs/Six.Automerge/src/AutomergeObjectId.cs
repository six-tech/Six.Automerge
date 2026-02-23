using Six.Automerge.FFI;
using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

/// <summary>
/// Managed representation of an Automerge object identifier (ExId).
/// </summary>
[PublicAPI]
public sealed class AutomergeObjectId : IEquatable<AutomergeObjectId>
{
  readonly byte[] _bytes;

  internal AutomergeObjectId(ReadOnlySpan<byte> bytes)
  {
    if(bytes.IsEmpty)
      throw new ArgumentException("Object id bytes must not be empty.", nameof(bytes));

    _bytes = bytes.ToArray();
  }

  /// <summary>
  /// The raw Automerge identifier bytes.
  /// </summary>
  public ReadOnlySpan<byte> Bytes => _bytes;

  /// <summary>
  /// Creates a hexadecimal string representation of the identifier.
  /// </summary>
  public override string ToString() => Convert.ToHexString(_bytes.AsSpan()).ToLowerInvariant();

  public bool Equals(AutomergeObjectId? other) => other is not null && Bytes.SequenceEqual(other.Bytes);

  public override bool Equals(object? obj) => obj is AutomergeObjectId other && Equals(other);

  public override int GetHashCode()
  {
    var hash = new HashCode();
    foreach(var b in _bytes)
    {
      hash.Add(b);
    }

    return hash.ToHashCode();
  }

  internal Native.AutomergeObjId ToNative()
  {
    return new Native.AutomergeObjId { bytes = Native.VecU8.From(_bytes.AsSpan()) };
  }

  internal static AutomergeObjectId FromNative(Native.AutomergeObjId native)
  {
    var bytes = native.bytes.ToArrayAndDispose();
    return new AutomergeObjectId(bytes);
  }

  internal static AutomergeObjectId FromBytes(ReadOnlySpan<byte> bytes) => new(bytes);
}
