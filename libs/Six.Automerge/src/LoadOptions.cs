// Copyright (c) Six. Licensed under the MIT License.

using Native = Six.Automerge.FFI.Native;

namespace Six.Automerge;

/// <summary>
/// Controls whether change hashes are verified during document load.
/// </summary>
[PublicAPI]
public enum VerificationMode
{
  /// <summary>
  /// Verify change hashes after loading (default).
  /// </summary>
  Check,

  /// <summary>
  /// Skip hash verification for faster loading.
  /// </summary>
  NoCheck
}

/// <summary>
/// Controls behavior when a document only partially loads.
/// </summary>
[PublicAPI]
public enum OnPartialLoad
{
  /// <summary>
  /// Return an error when the document only partially loads (default).
  /// </summary>
  Error,

  /// <summary>
  /// Ignore partial load errors and return whatever was loaded.
  /// </summary>
  Ignore
}

/// <summary>
/// Options for loading an Automerge document.
/// </summary>
[PublicAPI]
public sealed class LoadOptions
{
  /// <summary>
  /// Whether to verify change hashes after loading.
  /// Defaults to <see cref="VerificationMode.Check"/>.
  /// </summary>
  public VerificationMode VerificationMode { get; init; } = VerificationMode.Check;

  /// <summary>
  /// Behavior when the document only partially loads.
  /// Defaults to <see cref="OnPartialLoad.Error"/>.
  /// </summary>
  public OnPartialLoad OnPartialLoad { get; init; } = OnPartialLoad.Error;

  internal Native.AutomergeLoadOptions ToNative() => new()
  {
    verification_mode = VerificationMode switch
    {
      VerificationMode.Check => Native.AutomergeVerificationMode.Check,
      VerificationMode.NoCheck => Native.AutomergeVerificationMode.NoCheck,
      _ => Native.AutomergeVerificationMode.Check
    },
    on_partial_load = OnPartialLoad switch
    {
      OnPartialLoad.Error => Native.AutomergeOnPartialLoad.Error,
      OnPartialLoad.Ignore => Native.AutomergeOnPartialLoad.Ignore,
      _ => Native.AutomergeOnPartialLoad.Error
    }
  };
}
