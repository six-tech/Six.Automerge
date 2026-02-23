namespace Six.Automerge.Tests;

/// <summary>
/// Unit tests for the <see cref="ImmutableString"/> type.
/// </summary>
[Trait("Category", "ImmutableString")]
public sealed class ImmutableStringTests
{
  [Fact]
  public void Constructor_WithValidString_CreatesInstance()
  {
    // Arrange
    const string testValue = "https://example.com";

    // Act
    var immutable = new ImmutableString(testValue);

    // Assert
    Assert.Equal(testValue, immutable.ToString());
  }

  [Fact]
  public void Constructor_WithNull_ThrowsArgumentNullException()
  {
    // Arrange
    string? nullValue = null;

    // Act & Assert
#pragma warning disable CS8604 // Possible null reference argument - testing null handling
    var exception = Assert.Throws<ArgumentNullException>(() => new ImmutableString(nullValue));
#pragma warning restore CS8604
    Assert.Equal("value", exception.ParamName);
  }

  [Fact]
  public void ToString_ReturnsWrappedValue()
  {
    // Arrange
    const string expected = "test value";
    var immutable = new ImmutableString(expected);

    // Act
    var result = immutable.ToString();

    // Assert
    Assert.Equal(expected, result);
  }

  [Fact]
  public void ImplicitConversion_ToString_ReturnsWrappedValue()
  {
    // Arrange
    const string expected = "test value";
    var immutable = new ImmutableString(expected);

    // Act
    string result = immutable; // Implicit conversion

    // Assert
    Assert.Equal(expected, result);
  }

  [Fact]
  public void Equals_WithSameValue_ReturnsTrue()
  {
    // Arrange
    var first = new ImmutableString("same");
    var second = new ImmutableString("same");

    // Act & Assert
    Assert.True(first.Equals(second));
    Assert.True(second.Equals(first));
  }

  [Fact]
  public void Equals_WithDifferentValue_ReturnsFalse()
  {
    // Arrange
    var first = new ImmutableString("first");
    var second = new ImmutableString("second");

    // Act & Assert
    Assert.False(first.Equals(second));
    Assert.False(second.Equals(first));
  }

  [Fact]
  public void Equals_WithObject_WorksCorrectly()
  {
    // Arrange
    var immutable = new ImmutableString("test");
    object sameAsObject = new ImmutableString("test");
    object differentAsObject = new ImmutableString("different");
    object notImmutableString = "test";

    // Act & Assert
    Assert.True(immutable.Equals(sameAsObject));
    Assert.False(immutable.Equals(differentAsObject));
    Assert.False(immutable.Equals(notImmutableString));
    Assert.False(immutable.Equals(null));
  }

  [Fact]
  public void GetHashCode_WithSameValue_ReturnsSameHashCode()
  {
    // Arrange
    var first = new ImmutableString("same");
    var second = new ImmutableString("same");

    // Act
    var hash1 = first.GetHashCode();
    var hash2 = second.GetHashCode();

    // Assert
    Assert.Equal(hash1, hash2);
  }

  [Fact]
  public void GetHashCode_WithDifferentValue_ReturnsDifferentHashCode()
  {
    // Arrange
    var first = new ImmutableString("first");
    var second = new ImmutableString("second");

    // Act
    var hash1 = first.GetHashCode();
    var hash2 = second.GetHashCode();

    // Assert
    Assert.NotEqual(hash1, hash2);
  }

  [Fact]
  public void OperatorEquals_WithSameValue_ReturnsTrue()
  {
    // Arrange
    var first = new ImmutableString("same");
    var second = new ImmutableString("same");

    // Act & Assert
    Assert.True(first == second);
    Assert.True(second == first);
  }

  [Fact]
  public void OperatorEquals_WithDifferentValue_ReturnsFalse()
  {
    // Arrange
    var first = new ImmutableString("first");
    var second = new ImmutableString("second");

    // Act & Assert
    Assert.False(first == second);
    Assert.False(second == first);
  }

  [Fact]
  public void OperatorNotEquals_WithSameValue_ReturnsFalse()
  {
    // Arrange
    var first = new ImmutableString("same");
    var second = new ImmutableString("same");

    // Act & Assert
    Assert.False(first != second);
    Assert.False(second != first);
  }

  [Fact]
  public void OperatorNotEquals_WithDifferentValue_ReturnsTrue()
  {
    // Arrange
    var first = new ImmutableString("first");
    var second = new ImmutableString("second");

    // Act & Assert
    Assert.True(first != second);
    Assert.True(second != first);
  }

  [Fact]
  public void Constructor_WithEmptyString_CreatesInstance()
  {
    // Arrange
    const string emptyValue = "";

    // Act
    var immutable = new ImmutableString(emptyValue);

    // Assert
    Assert.Equal(emptyValue, immutable.ToString());
  }

  [Fact]
  public void Constructor_WithWhitespace_CreatesInstance()
  {
    // Arrange
    const string whitespaceValue = "   ";

    // Act
    var immutable = new ImmutableString(whitespaceValue);

    // Assert
    Assert.Equal(whitespaceValue, immutable.ToString());
  }

  [Fact]
  public void Constructor_WithUnicodeCharacters_CreatesInstance()
  {
    // Arrange
    const string unicodeValue = "Hello 世界 🌍";

    // Act
    var immutable = new ImmutableString(unicodeValue);

    // Assert
    Assert.Equal(unicodeValue, immutable.ToString());
  }

  [Fact]
  public void ImplicitConversion_InMethodCall_WorksCorrectly()
  {
    // Arrange
    var immutable = new ImmutableString("test");

    // Act
    var length = GetStringLength(immutable); // Implicit conversion

    // Assert
    Assert.Equal(4, length);
  }

  static int GetStringLength(string value) => value.Length;
}

/// <summary>
/// Unit tests for the <see cref="AutomergeExtensions.IsImmutableString"/> helper method.
/// </summary>
[Trait("Category", "ImmutableString")]
public sealed class IsImmutableStringTests
{
  [Fact]
  public void IsImmutableString_WithImmutableString_ReturnsTrue()
  {
    // Arrange
    object value = new ImmutableString("test");

    // Act
    var result = AutomergeExtensions.IsImmutableString(value);

    // Assert
    Assert.True(result);
  }

  [Fact]
  public void IsImmutableString_WithPlainString_ReturnsFalse()
  {
    // Arrange
    object value = "test";

    // Act
    var result = AutomergeExtensions.IsImmutableString(value);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void IsImmutableString_WithNull_ReturnsFalse()
  {
    // Arrange
    object? value = null;

    // Act
    var result = AutomergeExtensions.IsImmutableString(value);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void IsImmutableString_WithInt_ReturnsFalse()
  {
    // Arrange
    object value = 42;

    // Act
    var result = AutomergeExtensions.IsImmutableString(value);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void IsImmutableString_WithObject_ReturnsFalse()
  {
    // Arrange
    object value = new object();

    // Act
    var result = AutomergeExtensions.IsImmutableString(value);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void IsImmutableString_InIfStatement_WorksCorrectly()
  {
    // Arrange
    object immutableValue = new ImmutableString("test");
    object plainStringValue = "test";

    // Act & Assert
    if (AutomergeExtensions.IsImmutableString(immutableValue))
    {
      var casted = (ImmutableString)immutableValue;
      Assert.Equal("test", casted.ToString());
    }
    else
    {
      Assert.Fail("Should have detected ImmutableString");
    }

    Assert.False(AutomergeExtensions.IsImmutableString(plainStringValue));
  }
}
