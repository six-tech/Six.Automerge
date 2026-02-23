using Six.Automerge.FFI.Native;

namespace Six.Automerge.FFI;

/// <summary>
/// Exception thrown when an Automerge operation fails.
/// </summary>
[PublicAPI]
public class AutomergeException : Exception
{
  public AutomergeFfiErrorCode ErrorCode { get; }

  public AutomergeException(AutomergeFfiErrorCode code, string message)
    : base(message)
  {
    ErrorCode = code;
  }

  public AutomergeException(AutomergeFfiErrorCode code, string message, Exception innerException)
    : base(message, innerException)
  {
    ErrorCode = code;
  }

  internal static AutomergeException FromFfiError(AutomergeFfiError error)
  {
    using var message = error.message;
    return new AutomergeException(error.code, message.AsManagedString());
  }
}