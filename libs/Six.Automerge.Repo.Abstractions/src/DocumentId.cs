using Six.Automerge.Repository.Helpers;

namespace Six.Automerge.Repository;

/// <summary>
/// Represents a Base58Check-encoded Automerge document identifier.
/// Mirrors the branded string type from the TypeScript implementation.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public readonly record struct DocumentId
{
  /// <summary>
  /// Gets the encoded document identifier value.
  /// </summary>
  public string Value { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="DocumentId"/> struct.
  /// </summary>
  /// <param name="value">Base58Check-encoded document identifier.</param>
  /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
  public DocumentId(string value)
  {
    if(string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(value));

    Value = value;
  }

  /// <summary>
  /// Generates a new random document identifier.
  /// </summary>
  /// <returns>A new <see cref="DocumentId"/> value.</returns>
  public static DocumentId Generate()
  {
    var uuid = Guid.NewGuid().ToByteArray();
    return FromBinary(uuid);
  }

  /// <summary>
  /// Creates a <see cref="DocumentId"/> from its binary representation.
  /// </summary>
  /// <param name="bytes">Binary data to encode.</param>
  /// <returns>The encoded document identifier.</returns>
  /// <exception cref="ArgumentNullException">Thrown when bytes is null.</exception>
  /// <exception cref="ArgumentException">Thrown when bytes is empty.</exception>
  public static DocumentId FromBinary(byte[] bytes)
  {
    if(bytes is null)
      throw new ArgumentNullException(nameof(bytes));

    if(bytes.Length == 0)
      throw new ArgumentException("Document ID binary representation cannot be empty.", nameof(bytes));

    if(bytes.Length < 16)
      throw new ArgumentException("Document ID binary representation must be at least 16 bytes.", nameof(bytes));

    var encoded = Base58Check.Encode(bytes);
    return new DocumentId(encoded);
  }

  /// <summary>
  /// Creates a <see cref="DocumentId"/> from its binary representation.
  /// </summary>
  /// <param name="bytes">Binary data to encode.</param>
  /// <returns>The encoded document identifier.</returns>
  /// <exception cref="ArgumentException">Thrown when bytes is empty.</exception>
  public static DocumentId FromBinary(ReadOnlySpan<byte> bytes)
  {
    if(bytes.Length == 0)
      throw new ArgumentException("Document ID binary representation cannot be empty.", nameof(bytes));

    if(bytes.Length < 16)
      throw new ArgumentException("Document ID binary representation must be at least 16 bytes.", nameof(bytes));

    var buffer = bytes.ToArray();
    return FromBinary(buffer);
  }

  /// <summary>
  /// Converts a document identifier to its binary representation.
  /// </summary>
  /// <param name="documentId">Document identifier to convert.</param>
  /// <returns>The binary representation.</returns>
  public static byte[] ToBinary(DocumentId documentId)
  {
    return documentId.ToBinary();
  }

  /// <summary>
  /// Converts a document identifier string to its binary representation.
  /// </summary>
  /// <param name="value">Document identifier value.</param>
  /// <returns>The binary representation.</returns>
  public static byte[] ToBinary(string value)
  {
    return Parse(value).ToBinary();
  }

  /// <summary>
  /// Parses a string into a <see cref="DocumentId"/>.
  /// </summary>
  /// <param name="value">The encoded document identifier.</param>
  /// <returns>The parsed <see cref="DocumentId"/>.</returns>
  /// <exception cref="ArgumentException">Thrown when value is null or whitespace.</exception>
  /// <exception cref="FormatException">Thrown when value is not a valid document identifier.</exception>
  public static DocumentId Parse(string value)
  {
    if(string.IsNullOrWhiteSpace(value))
      throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(value));

    if(!TryParse(value, out var documentId))
      throw new FormatException($"Invalid document ID: {value}");

    return documentId;
  }

  /// <summary>
  /// Attempts to parse a string into a <see cref="DocumentId"/>.
  /// </summary>
  /// <param name="value">Value to parse.</param>
  /// <param name="documentId">Resulting document identifier.</param>
  /// <returns>True when the value is a valid document identifier.</returns>
  public static bool TryParse(string? value, out DocumentId documentId)
  {
    if(string.IsNullOrWhiteSpace(value))
    {
      documentId = default;
      return false;
    }

    try
    {
      var binary = Base58Check.Decode(value);
      if(binary.Length < 16)
      {
        documentId = default;
        return false;
      }

      documentId = new DocumentId(value);
      return true;
    }
    catch
    {
      documentId = default;
      return false;
    }
  }

  /// <summary>
  /// Determines if the provided value is a valid document identifier.
  /// </summary>
  /// <param name="value">Value to validate.</param>
  /// <returns>True when the value is valid.</returns>
  public static bool IsValid(string? value)
  {
    return TryParse(value, out _);
  }

  /// <summary>
  /// Converts the document identifier to its binary representation.
  /// </summary>
  /// <returns>Binary representation of the identifier.</returns>
  public byte[] ToBinary()
  {
    return Base58Check.Decode(Value);
  }

  /// <inheritdoc />
  public override string ToString()
  {
    return Value;
  }

  /// <summary>
  /// Implicit conversion to string for interop with existing APIs.
  /// </summary>
  /// <param name="documentId">Document identifier.</param>
  public static implicit operator string(DocumentId documentId)
  {
    return documentId.Value;
  }

  /// <summary>
  /// Implicit conversion from string to <see cref="DocumentId"/>.
  /// </summary>
  /// <param name="value">Document identifier value.</param>
  /// <exception cref="FormatException">Thrown when value is not a valid identifier.</exception>
  public static implicit operator DocumentId(string value)
  {
    return Parse(value);
  }
}
