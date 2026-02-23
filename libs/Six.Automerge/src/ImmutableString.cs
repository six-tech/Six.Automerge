using System.Diagnostics.CodeAnalysis;

namespace Six.Automerge;

/// <summary>
/// Represents an immutable string value that should be replaced atomically rather than merged character-by-character.
/// </summary>
/// <remarks>
/// <para>
/// In Automerge, regular strings are collaborative sequences where concurrent character edits can be merged.
/// However, some strings (like URLs, IDs, or enum values) should always be replaced entirely rather than merged.
/// </para>
/// <para>
/// Use <see cref="ImmutableString"/> to signal this intent and prevent unintended character-level merging.
/// This aligns with the Automerge JavaScript SDK's <c>ImmutableString</c> type (formerly <c>RawString</c>).
/// </para>
/// <para>
/// <b>When to use:</b>
/// <list type="bullet">
///   <item><description>URLs and identifiers that must remain intact</description></item>
///   <item><description>Enum-like string constants (e.g., "pending", "approved", "rejected")</description></item>
///   <item><description>Foreign keys and database references</description></item>
///   <item><description>Configuration values that should be updated atomically</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var doc = repo.Create&lt;MyDocument&gt;();
///
/// // Create immutable string for URL
/// doc.Change(d =&gt; {
///     d.websiteUrl = new ImmutableString("https://example.com");
///     d.status = new ImmutableString("active");
/// });
///
/// // Read back
/// ImmutableString url = doc.State.websiteUrl;
/// string urlString = url; // Implicit conversion to string
/// </code>
/// </example>
[PublicAPI]
public readonly struct ImmutableString : IEquatable<ImmutableString>
{
  readonly string _value;

  /// <summary>
  /// Initializes a new instance of the <see cref="ImmutableString"/> struct with the specified value.
  /// </summary>
  /// <param name="value">The string value to wrap. Cannot be null.</param>
  /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
  public ImmutableString(string value)
  {
    _value = value ?? throw new ArgumentNullException(nameof(value));
  }

  /// <summary>
  /// Returns the wrapped string value.
  /// </summary>
  /// <returns>The wrapped string value.</returns>
  public override string ToString() => _value;

  /// <summary>
  /// Implicitly converts an <see cref="ImmutableString"/> to a <see cref="string"/>.
  /// </summary>
  /// <param name="immutable">The immutable string to convert.</param>
  /// <returns>The wrapped string value.</returns>
  public static implicit operator string(ImmutableString immutable) => immutable._value;

  /// <summary>
  /// Determines whether the specified <see cref="ImmutableString"/> is equal to the current instance.
  /// </summary>
  /// <param name="other">The immutable string to compare with the current instance.</param>
  /// <returns>
  /// <see langword="true"/> if the specified immutable string is equal to the current instance; otherwise, <see langword="false"/>.
  /// </returns>
  public bool Equals(ImmutableString other) => _value == other._value;

  /// <summary>
  /// Determines whether the specified object is equal to the current instance.
  /// </summary>
  /// <param name="obj">The object to compare with the current instance.</param>
  /// <returns>
  /// <see langword="true"/> if the specified object is an <see cref="ImmutableString"/> and is equal to the current instance;
  /// otherwise, <see langword="false"/>.
  /// </returns>
  public override bool Equals([NotNullWhen(true)] object? obj) =>
    obj is ImmutableString other && Equals(other);

  /// <summary>
  /// Returns the hash code for the current instance.
  /// </summary>
  /// <returns>A hash code for the current instance.</returns>
  public override int GetHashCode() => _value.GetHashCode();

  /// <summary>
  /// Determines whether two specified <see cref="ImmutableString"/> instances are equal.
  /// </summary>
  /// <param name="left">The first immutable string to compare.</param>
  /// <param name="right">The second immutable string to compare.</param>
  /// <returns>
  /// <see langword="true"/> if <paramref name="left"/> is equal to <paramref name="right"/>; otherwise, <see langword="false"/>.
  /// </returns>
  public static bool operator ==(ImmutableString left, ImmutableString right) =>
    left.Equals(right);

  /// <summary>
  /// Determines whether two specified <see cref="ImmutableString"/> instances are not equal.
  /// </summary>
  /// <param name="left">The first immutable string to compare.</param>
  /// <param name="right">The second immutable string to compare.</param>
  /// <returns>
  /// <see langword="true"/> if <paramref name="left"/> is not equal to <paramref name="right"/>; otherwise, <see langword="false"/>.
  /// </returns>
  public static bool operator !=(ImmutableString left, ImmutableString right) =>
    !left.Equals(right);
}
