using Six.Automerge.FFI;
using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

[PublicAPI]
public sealed class AutomergeSyncState : IDisposable
{
  readonly Native.AutomergeSyncState _state;
  bool _disposed;

  AutomergeSyncState(Native.AutomergeSyncState state)
  {
    _state = state;
  }

  void ThrowIfDisposed()
  {
    if(_disposed)
    {
      throw new ObjectDisposedException(nameof(AutomergeSyncState));
    }
  }

  internal IntPtr Context
  {
    get
    {
      ThrowIfDisposed();
      return _state.Context;
    }
  }

  public static AutomergeSyncState Create() => new(Native.AutomergeSyncState.New());

  public static AutomergeSyncState Decode(byte[] encoded)
  {
    using var slice = Native.SliceU8.From(encoded);
    var native = Native.AutomergeSyncState.Decode(slice);
    return new AutomergeSyncState(native);
  }

  public byte[] Encode()
  {
    ThrowIfDisposed();
    using var bytes = _state.Encode();
    return bytes.AsManagedBytes();
  }

  public void Reset()
  {
    ThrowIfDisposed();
    _state.Reset();
  }

  public IReadOnlyList<string> SharedHeads
  {
    get
    {
      ThrowIfDisposed();
      using var vec = _state.SharedHeads();
      return vec.AsHashList().Select(h => Convert.ToHexString(h.AsManagedBytes()).ToLowerInvariant()).ToArray();
    }
  }

  public void Dispose()
  {
    if(_disposed)
    {
      return;
    }

    _disposed = true;
    _state.Dispose();
  }
}
