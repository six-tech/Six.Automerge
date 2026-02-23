using Six.Automerge.FFI.Native;

namespace Six.Automerge.FFI;

/// <summary>
/// Extension methods for safely unwrapping FFI Result types.
/// </summary>
internal static class ResultExtensions
{
  public static IntPtr UnwrapOrThrow(this ResultConstPtrAutomergeDocAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static void UnwrapOrThrow(this ResultAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return;
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static T UnwrapOrThrow<T>(this ResultU64AutomergeFfiError result) where T : struct
  {
    if(result.IsOk)
    {
      var value = result.AsOk();
      return (T)(object)value;
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static ulong UnwrapU64OrThrow(this ResultU64AutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static AutomergeObjectType UnwrapOrThrow(this ResultAutomergeObjectTypeAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static OptionChangeHashBytes UnwrapOrThrow(this ResultOptionChangeHashBytesAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static VecChangeHashBytes UnwrapOrThrow(this ResultVecChangeHashBytesAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static VecChangeBytes UnwrapOrThrow(this ResultVecChangeBytesAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static AutomergeObjId UnwrapOrThrow(this ResultAutomergeObjIdAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static VecUtf8String UnwrapOrThrow(this ResultVecUtf8StringAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static Utf8String UnwrapOrThrow(this ResultUtf8StringAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }

  public static OptionByteArray UnwrapOrThrow(this ResultOptionByteArrayAutomergeFfiError result)
  {
    if(result.IsOk)
    {
      return result.AsOk();
    }

    if(result.IsErr)
    {
      var error = result.AsErr();
      var message = error.message.AsManagedString();
      throw new AutomergeException(error.code, message);
    }

    if(result.IsPanic)
    {
      throw new AutomergeException(
        AutomergeFfiErrorCode.Unknown,
        "Native library panicked");
    }

    throw new AutomergeException(
      AutomergeFfiErrorCode.NullPointer,
      "Unexpected null result from native library");
  }
}