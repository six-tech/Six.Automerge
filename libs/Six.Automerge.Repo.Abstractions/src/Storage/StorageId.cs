namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Identifies a storage location or peer for sync state persistence.
/// Typically derived from a peer ID or storage adapter identifier.
/// </summary>
[PublicAPI]
public readonly record struct StorageId
{
  readonly string _value;

  /// <summary>
  /// Initializes a new instance of <see cref="StorageId"/> with the specified value.
  /// </summary>
  /// <param name="value">The storage identifier value.</param>
  /// <exception cref="ArgumentException">Thrown if the value is null or whitespace.</exception>
  public StorageId(string value)
  {
    if(string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("StorageId cannot be null or whitespace", nameof(value));
    _value = value;
  }

  /// <summary>
  /// Implicitly converts a <see cref="StorageId"/> to a string.
  /// </summary>
  /// <param name="id">The storage identifier.</param>
  public static implicit operator string(StorageId id) => id._value;

  /// <summary>
  /// Implicitly converts a string to a <see cref="StorageId"/>.
  /// </summary>
  /// <param name="value">The string value.</param>
  public static implicit operator StorageId(string value) => new(value);

  /// <summary>
  /// Returns the string representation of this storage identifier.
  /// </summary>
  /// <returns>The storage identifier as a string.</returns>
  public override string ToString() => _value;
}