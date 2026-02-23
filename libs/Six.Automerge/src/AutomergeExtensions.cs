namespace Six.Automerge;

/// <summary>
/// Provides extension methods and helper functions for working with Automerge types.
/// </summary>
[PublicAPI]
public static class AutomergeExtensions
{
  /// <summary>
  /// Determines whether the specified object is an <see cref="ImmutableString"/>.
  /// </summary>
  /// <param name="value">The object to check.</param>
  /// <returns>
  /// <see langword="true"/> if <paramref name="value"/> is an <see cref="ImmutableString"/>; otherwise, <see langword="false"/>.
  /// </returns>
  /// <remarks>
  /// This helper method provides a convenient way to identify <see cref="ImmutableString"/> instances
  /// without explicit type casting or pattern matching.
  /// </remarks>
  /// <example>
  /// <code>
  /// object value = new ImmutableString("test");
  ///
  /// if (AutomergeExtensions.IsImmutableString(value))
  /// {
  ///     var immutable = (ImmutableString)value;
  ///     Console.WriteLine($"Atomic string: {immutable}");
  /// }
  /// </code>
  /// </example>
  public static bool IsImmutableString(object? value) => value is ImmutableString;
}
