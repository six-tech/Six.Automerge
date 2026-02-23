using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Six.Automerge.FFI;

public static class InteropExtensions
{
  public static string AsManagedString(this Native.Utf8String value)
  {
    try
    {
      return value.String;
    }
    finally
    {
      value.Dispose();
    }
  }

  public static byte[] AsManagedBytes(this Native.ByteArray value)
  {
    try
    {
      return CopyVecU8(value.data, disposeVec: false);
    }
    finally
    {
      value.Dispose();
    }
  }

  public static byte[]? AsManagedBytes(this Native.OptionByteArray option)
  {
    try
    {
      if(option.IsSome)
      {
        using var some = option.AsSome();
        return some.AsManagedBytes();
      }

      return null;
    }
    finally
    {
      option.Dispose();
    }
  }

  public static byte[] AsManagedBytes(this Native.ChangeBytes value)
  {
    try
    {
      return CopyVecU8(value.bytes, disposeVec: false);
    }
    finally
    {
      value.Dispose();
    }
  }

  public static byte[] AsManagedBytes(this Native.ChangeHashBytes value)
  {
    byte[] bytes = new byte[32];
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0, 8), value.part1);
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), value.part2);
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16, 8), value.part3);
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24, 8), value.part4);
    return bytes;
  }

  public static byte[]? AsManagedBytes(this Native.OptionChangeBytes option)
  {
    try
    {
      if(option.IsSome)
      {
        using var change = option.AsSome();
        return change.AsManagedBytes();
      }

      return null;
    }
    finally
    {
      option.Dispose();
    }
  }

  public static byte[] ToArrayAndDispose(this Native.VecU8 vec) => CopyVecU8(vec, disposeVec: true);

  public static IReadOnlyList<string> AsStringList(this Native.VecUtf8String vec)
  {
    var length = vec.Count;
    if(length == 0)
    {
      return Array.Empty<string>();
    }

    var result = new List<string>(length);
    for(var i = 0; i < length; i++)
    {
      using var managed = vec[i];
      try
      {
        result.Add(managed.String);
      }
      finally
      {
        managed.Dispose();
      }
    }

    return result;
  }

  public static IReadOnlyList<Native.ChangeHashBytes> AsHashList(this Native.VecChangeHashBytes vec)
  {
    if(vec.Count == 0)
    {
      return Array.Empty<Native.ChangeHashBytes>();
    }

    var length = vec.Count;
    var result = new Native.ChangeHashBytes[length];
    for(var i = 0; i < length; i++)
    {
      result[i] = vec[i];
    }

    return result;
  }

  public static IReadOnlyList<byte[]> AsByteArrayList(this Native.VecChangeBytes vec)
  {
    if(vec.Count == 0)
    {
      return Array.Empty<byte[]>();
    }

    return CopyChangeBytesVector(vec);
  }

  
  
  static byte[] CopyVecU8(Native.VecU8 vec, bool disposeVec)
  {
    try
    {
      if(vec._ptr == IntPtr.Zero || vec._len == 0)
      {
        return Array.Empty<byte>();
      }

      var length = checked((int)vec._len);
      var buffer = new byte[length];
      Marshal.Copy(vec._ptr, buffer, 0, length);
      return buffer;
    }
    finally
    {
      if(disposeVec)
      {
        vec.Dispose();
      }
    }
  }

  static byte[][] CopyChangeBytesVector(Native.VecChangeBytes vec)
  {
    var count = vec.Count;
    if(count == 0)
    {
      return Array.Empty<byte[]>();
    }

    var result = new byte[count][];
    var structSize = Marshal.SizeOf<Native.ChangeBytes.Unmanaged>();

    for(var i = 0; i < count; i++)
    {
      var elementPtr = IntPtr.Add(vec._ptr, i * structSize);
      var unmanaged = Marshal.PtrToStructure<Native.ChangeBytes.Unmanaged>(elementPtr);
      if(unmanaged.bytes._ptr == IntPtr.Zero || unmanaged.bytes._len == 0)
      {
        result[i] = Array.Empty<byte>();
        continue;
      }

      var length = checked((int)unmanaged.bytes._len);
      var target = new byte[length];
      Marshal.Copy(unmanaged.bytes._ptr, target, 0, length);
      result[i] = target;
    }

    return result;
  }
}