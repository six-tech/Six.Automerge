namespace Six.Automerge.Repository;

/// <summary>
/// Utilities for parsing and formatting Automerge URLs.
/// Format: automerge:&lt;documentId&gt;[#head1|head2|...]
/// </summary>
/// <example>
/// <code>
/// // Generate new URL
/// var url = AutomergeUrl.Generate();
/// // Result: "automerge:4NMNnkMhL8jXrdJ9jamS58PAVdXu"
///
/// // Parse URL
/// var parsed = AutomergeUrl.Parse(url);
/// Console.WriteLine(parsed.DocumentId); // "4NMNnkMhL8jXrdJ9jamS58PAVdXu"
///
/// // Format with heads
/// var urlWithHeads = AutomergeUrl.Format("4NMNnkMhL8jXrdJ9jamS58PAVdXu", new[] { "head1", "head2" });
/// // Result: "automerge:4NMNnkMhL8jXrdJ9jamS58PAVdXu#head1|head2"
/// </code>
/// </example>
[PublicAPI]
public static class AutomergeUrl
{
  const string UrlScheme = "automerge:";

  /// <summary>
  /// Parses an Automerge URL into document ID and optional heads.
  /// </summary>
  /// <param name="url">The Automerge URL to parse.</param>
  /// <returns>A parsed URL containing document ID and heads.</returns>
  /// <exception cref="ArgumentException">Thrown if URL is null or whitespace.</exception>
  /// <exception cref="FormatException">Thrown if URL format is invalid.</exception>
  public static ParsedAutomergeUrl Parse(string url)
  {
    if(string.IsNullOrWhiteSpace(url))
      throw new ArgumentException("URL cannot be null or whitespace", nameof(url));

    if(!url.StartsWith(UrlScheme, StringComparison.OrdinalIgnoreCase))
      throw new FormatException($"URL must start with '{UrlScheme}'");

    var urlBody = url.Substring(UrlScheme.Length);

    // Split on # for heads
    var parts = urlBody.Split('#', 2);
    if(string.IsNullOrWhiteSpace(parts[0]))
      throw new FormatException("URL must contain a document ID");

    var documentId = DocumentId.Parse(parts[0]);

    // Parse heads if present
    IReadOnlyList<string>? heads = null;
    if(parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
    {
      heads = parts[1].Split('|', StringSplitOptions.RemoveEmptyEntries);
    }

    return new ParsedAutomergeUrl(documentId, heads);
  }

  /// <summary>
  /// Formats a document ID and optional heads into an Automerge URL.
  /// </summary>
  /// <param name="documentId">The document ID.</param>
  /// <param name="heads">Optional heads to include in the URL.</param>
  /// <returns>A formatted Automerge URL.</returns>
  /// <exception cref="ArgumentException">Thrown if documentId is invalid.</exception>
  public static string Format(DocumentId documentId, IEnumerable<string>? heads = null)
  {
    var url = $"{UrlScheme}{documentId}";

    if(heads is not null)
    {
      var headsList = heads.ToList();
      if(headsList.Count > 0)
      {
        url += "#" + string.Join("|", headsList);
      }
    }

    return url;
  }

  /// <summary>
  /// Formats a document ID provided as string.
  /// </summary>
  /// <param name="documentId">Document identifier value.</param>
  /// <param name="heads">Optional heads.</param>
  /// <returns>Formatted Automerge URL.</returns>
  public static string Format(string documentId, IEnumerable<string>? heads = null)
  {
    return Format(DocumentId.Parse(documentId), heads);
  }

  /// <summary>
  /// Checks if a string is a valid Automerge URL.
  /// </summary>
  /// <param name="url">The URL to validate.</param>
  /// <returns>True if the URL is valid, false otherwise.</returns>
  public static bool IsValid(string url)
  {
    try
    {
      Parse(url);
      return true;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Extracts just the document ID from an Automerge URL.
  /// </summary>
  /// <param name="url">The Automerge URL.</param>
  /// <returns>The document ID.</returns>
  /// <exception cref="ArgumentException">Thrown if URL is null or whitespace.</exception>
  /// <exception cref="FormatException">Thrown if URL format is invalid.</exception>
  public static DocumentId GetDocumentId(string url)
  {
    return Parse(url).DocumentId;
  }

  /// <summary>
  /// Generates a new Automerge URL with a random document ID.
  /// </summary>
  /// <returns>A new Automerge URL.</returns>
  public static string Generate()
  {
    return Format(DocumentId.Generate());
  }
}

/// <summary>
/// Result of parsing an Automerge URL.
/// </summary>
/// <param name="DocumentId">The document ID extracted from the URL.</param>
/// <param name="Heads">The heads extracted from the URL, or null if not present.</param>
[PublicAPI]
public sealed record ParsedAutomergeUrl(
  DocumentId DocumentId,
  IReadOnlyList<string>? Heads);
