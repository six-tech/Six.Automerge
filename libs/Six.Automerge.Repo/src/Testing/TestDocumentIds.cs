using Six.Automerge.Repository;

namespace Six.Automerge.Repo.Testing;

/// <summary>
/// Test helper class for generating DocumentId values in unit tests.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class TestDocumentIds
{
  /// <summary>
  /// Default test document ID string.
  /// </summary>
  public const string Default = "4NMNnkMhL8jXrdJ9jamS58PAVdXu";

  /// <summary>
  /// Alternate test document ID string.
  /// </summary>
  public const string Alternate = "uKK1dJ4vE3E6r27kz5bsFaCykvM";

  /// <summary>
  /// Tertiary test document ID string.
  /// </summary>
  public const string Tertiary = "7s2oTNNZh9GsARWX7xWXT14BMWZwu";

  /// <summary>
  /// Default test document ID as DocumentId value.
  /// </summary>
  public static DocumentId DefaultId => DocumentId.Parse(Default);

  /// <summary>
  /// Alternate test document ID as DocumentId value.
  /// </summary>
  public static DocumentId AlternateId => DocumentId.Parse(Alternate);

  /// <summary>
  /// Tertiary test document ID as DocumentId value.
  /// </summary>
  public static DocumentId TertiaryId => DocumentId.Parse(Tertiary);

  /// <summary>
  /// Generates a unique document ID string for testing.
  /// </summary>
  /// <returns>A new unique DocumentId string.</returns>
  public static string UniqueString()
  {
    return DocumentId.Generate().Value;
  }

  /// <summary>
  /// Generates a unique DocumentId value for testing.
  /// </summary>
  /// <returns>A new unique DocumentId.</returns>
  public static DocumentId Unique()
  {
    return DocumentId.Generate();
  }

  /// <summary>
  /// Creates a DocumentId from a string value.
  /// </summary>
  /// <param name="value">The document ID string.</param>
  /// <returns>A DocumentId instance.</returns>
  public static DocumentId FromString(string value)
  {
    return new DocumentId(value);
  }
}
