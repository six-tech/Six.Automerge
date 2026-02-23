namespace Six.Automerge.Repository.Network;

/// <summary>
/// Uniquely identifies a peer in the network.
/// </summary>
[PublicAPI]
public readonly record struct PeerId
{
  readonly string _value;

  public PeerId(string value)
  {
    if(string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("PeerId cannot be null or whitespace", nameof(value));
    _value = value;
  }

  /// <summary>
  /// Generates a new unique PeerId with format "peer-{random}".
  /// Mirrors the TypeScript implementation which uses Math.random().toString(36).slice(4).
  /// </summary>
  public static PeerId Generate()
  {
    var guid = Guid.NewGuid();
    var randomStr = guid.ToString("N").Substring(0, 8);
    return new($"peer-{randomStr}");
  }

  public static implicit operator string(PeerId id) => id._value;
  public static implicit operator PeerId(string value) => new(value);

  public override string ToString() => _value;
}