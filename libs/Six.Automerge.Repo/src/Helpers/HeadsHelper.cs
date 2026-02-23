namespace Six.Automerge.Repository.Helpers;

/// <summary>
/// Helper methods for working with Automerge heads.
/// </summary>
internal static class HeadsHelper
{
  /// <summary>
  /// Checks if two head lists are equal (order-independent).
  /// </summary>
  /// <param name="heads1">First list of heads.</param>
  /// <param name="heads2">Second list of heads.</param>
  /// <returns>True if the head lists contain the same elements, false otherwise.</returns>
  public static bool AreSame(IReadOnlyList<string>? heads1, IReadOnlyList<string>? heads2)
  {
    if(heads1 is null && heads2 is null) return true;
    if(heads1 is null || heads2 is null) return false;
    if(heads1.Count != heads2.Count) return false;

    var set1 = new HashSet<string>(heads1);
    var set2 = new HashSet<string>(heads2);

    return set1.SetEquals(set2);
  }

  /// <summary>
  /// Converts heads to a deterministic string for hashing/comparison.
  /// Heads are sorted alphabetically to ensure consistent output.
  /// </summary>
  /// <param name="heads">The list of heads.</param>
  /// <returns>A pipe-separated string of sorted heads.</returns>
  public static string ToHeadsKey(IReadOnlyList<string> heads)
  {
    if(heads.Count == 0)
      return string.Empty;

    // Sort heads for deterministic output
    var sorted = heads.OrderBy(h => h, StringComparer.Ordinal).ToList();
    return string.Join("|", sorted);
  }

  /// <summary>
  /// Parses a heads key back into a list of heads.
  /// </summary>
  /// <param name="headsKey">The pipe-separated heads key.</param>
  /// <returns>A list of heads.</returns>
  public static IReadOnlyList<string> FromHeadsKey(string headsKey)
  {
    if(string.IsNullOrEmpty(headsKey))
      return Array.Empty<string>();

    return headsKey.Split('|', StringSplitOptions.RemoveEmptyEntries);
  }
}
