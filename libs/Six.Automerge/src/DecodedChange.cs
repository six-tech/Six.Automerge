using System;
using System.Collections.Generic;
using System.Linq;

namespace Six.Automerge;

/// <summary>
/// Represents a decoded Automerge change with its metadata.
/// </summary>
/// <remarks>
/// This structure provides detailed information about a specific change in an Automerge document,
/// including the actor who made the change, when it was made, and what other changes it depends on.
/// </remarks>
[PublicAPI]
public sealed class DecodedChange
{
  /// <summary>
  /// Gets the hash of this change, represented as a lowercase hex string.
  /// </summary>
  public string Hash { get; init; }

  /// <summary>
  /// Gets the actor ID who created this change.
  /// </summary>
  public string Actor { get; init; }

  /// <summary>
  /// Gets the sequence number of this change for the actor.
  /// </summary>
  public ulong Sequence { get; init; }

  /// <summary>
  /// Gets the timestamp when this change was made, in Unix milliseconds.
  /// </summary>
  public long TimestampMillis { get; init; }

  /// <summary>
  /// Gets the optional commit message associated with this change.
  /// </summary>
  public string? Message { get; init; }

  /// <summary>
  /// Gets the list of change hashes that this change depends on.
  /// </summary>
  public IReadOnlyList<string> Dependencies { get; init; }

  /// <summary>
  /// Initializes a new instance of the <see cref="DecodedChange"/> class.
  /// </summary>
  public DecodedChange(
    string hash,
    string actor,
    ulong sequence,
    long timestampMillis,
    string? message,
    IReadOnlyList<string> dependencies)
  {
    Hash = hash ?? throw new ArgumentNullException(nameof(hash));
    Actor = actor ?? throw new ArgumentNullException(nameof(actor));
    Sequence = sequence;
    TimestampMillis = timestampMillis;
    Message = message;
    Dependencies = dependencies ?? Array.Empty<string>();
  }

  /// <summary>
  /// Returns a string representation of this decoded change for debugging purposes.
  /// </summary>
  public override string ToString()
  {
    var msg = string.IsNullOrEmpty(Message) ? "" : $" \"{Message}\"";
    var deps = Dependencies.Count > 0 ? $" (deps: {Dependencies.Count})" : "";
    return $"Change {Hash.Substring(0, Math.Min(8, Hash.Length))} by {Actor} seq={Sequence}{msg}{deps}";
  }
}
