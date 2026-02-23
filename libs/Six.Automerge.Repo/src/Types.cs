namespace Six.Automerge.Repository;

/// <summary>
/// A collection of Base58Check-encoded heads (hashes) from an Automerge document.
/// Mirrors the branded type from the TypeScript implementation.
/// </summary>
[PublicAPI]
public sealed class UrlHeads : List<string>
{
  /// <summary>
  /// Creates a new UrlHeads from an enumerable of head strings.
  /// </summary>
  /// <param name="heads">The head strings to include.</param>
  public UrlHeads(IEnumerable<string>? heads = null) : base(heads ?? [])
  {
  }

  /// <summary>
  /// Creates an empty UrlHeads.
  /// </summary>
  public UrlHeads() : base()
  {
  }
}
