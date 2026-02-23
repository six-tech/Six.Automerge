namespace Six.Automerge.Repository.Network;

/// <summary>
/// Identifies a session for ephemeral message deduplication.
/// A new session ID should be generated each time a peer starts.
/// </summary>
[PublicAPI]
public readonly record struct SessionId
{
  readonly string _value;

  public SessionId(string value)
  {
    if(string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("SessionId cannot be null or whitespace", nameof(value));
    _value = value;
  }

  /// <summary>
  /// Generates a new unique SessionId using a GUID.
  /// </summary>
  public static SessionId Generate() => new(Guid.NewGuid().ToString("N"));

  public static implicit operator string(SessionId id) => id._value;
  public static implicit operator SessionId(string value) => new(value);

  public override string ToString() => _value;
}