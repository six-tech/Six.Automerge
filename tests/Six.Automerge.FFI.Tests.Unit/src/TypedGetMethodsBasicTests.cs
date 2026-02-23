using Six.Automerge;
using Xunit;

namespace Six.Automerge.FFI.Tests.Unit;

/// <summary>
/// Basic tests for typed Get methods on AutomergeDocument.
/// </summary>
[Trait("Category", "GetMethods")]
public class TypedGetMethodsBasicTests : IDisposable
{
  AutomergeDocument _doc;
  AutomergeObjectId _root;

  public TypedGetMethodsBasicTests()
  {
    _doc = AutomergeDocument.Create();
    _root = _doc.RootId;
  }

  public void Dispose()
  {
    _doc?.Dispose();
  }

  [Fact]
  public void GetString_WithValidValue_ReturnsValue()
  {
    // Arrange
    _doc.PutString(_root, "name", "Alice");
    _doc.Commit();

    // Act
    var result = _doc.GetString(_root, "name");

    // Assert
    Assert.Equal("Alice", result);
  }

  [Fact]
  public void GetString_MissingKey_ReturnsNull()
  {
    // Act
    var result = _doc.GetString(_root, "nonexistent");

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void GetString_TypeMismatch_ReturnsNull()
  {
    // Arrange
    _doc.PutInt64(_root, "count", 42);

    // Act
    var result = _doc.GetString(_root, "count");

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void GetImmutableString_WithValidValue_ReturnsImmutableString()
  {
    // Arrange
    var url = new ImmutableString("https://example.com");
    _doc.PutImmutableString(_root, "url", url);
    _doc.Commit();

    // Act
    var result = _doc.GetImmutableString(_root, "url");

    // Assert
    Assert.NotNull(result);
    Assert.Equal(url, result.Value);
  }

  [Fact]
  public void GetImmutableString_AfterPutString_Works()
  {
    // Arrange - Store with PutString
    _doc.PutString(_root, "data", "test");
    _doc.Commit();

    // Act - Retrieve with GetImmutableString
    var result = _doc.GetImmutableString(_root, "data");

    // Assert
    Assert.NotNull(result);
    Assert.Equal(new ImmutableString("test"), result.Value);
  }

  [Fact]
  public void GetInt64_WithValidValue_ReturnsValue()
  {
    // Arrange
    _doc.PutInt64(_root, "count", 42);
    _doc.Commit();

    // Act
    var result = _doc.GetInt64(_root, "count");

    // Assert
    Assert.Equal(42, result);
  }

  [Fact]
  public void GetInt64_MissingKey_ReturnsNull()
  {
    // Act
    var result = _doc.GetInt64(_root, "nonexistent");

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void GetBoolean_WithValidValue_ReturnsValue()
  {
    // Arrange
    _doc.PutBoolean(_root, "active", true);
    _doc.Commit();

    // Act
    var result = _doc.GetBoolean(_root, "active");

    // Assert
    Assert.True(result);
  }

  [Fact]
  public void GetDouble_WithValidValue_ReturnsValue()
  {
    // Arrange
    _doc.PutDouble(_root, "pi", 3.14159);
    _doc.Commit();

    // Act
    var result = _doc.GetDouble(_root, "pi");

    // Assert
    Assert.Equal(3.14159, result);
  }

  [Fact]
  public void GetBytes_WithValidValue_ReturnsBytes()
  {
    // Arrange
    byte[] data = [1, 2, 3, 4, 5];
    _doc.PutBytes(_root, "data", data);
    _doc.Commit();

    // Act
    var result = _doc.GetBytes(_root, "data");

    // Assert
    Assert.NotNull(result);
    Assert.Equal(data, result);
  }

  [Fact]
  public void GetTimestamp_WithValidValue_ReturnsValue()
  {
    // Arrange
    long timestamp = 1698765432000;
    _doc.PutTimestamp(_root, "created", timestamp);
    _doc.Commit();

    // Act
    var result = _doc.GetTimestamp(_root, "created");

    // Assert
    Assert.Equal(timestamp, result);
  }

  [Fact]
  public void GetCounter_WithValidValue_ReturnsValue()
  {
    // Arrange
    _doc.PutCounter(_root, "views", 100);
    _doc.Commit();

    // Act
    var result = _doc.GetCounter(_root, "views");

    // Assert
    Assert.Equal(100, result);
  }

  [Fact]
  public void AllGetMethods_RoundTripPreservesValues()
  {
    // Arrange & Act & Assert multiple types
    _doc.PutString(_root, "str", "test");
    Assert.Equal("test", _doc.GetString(_root, "str"));

    _doc.PutInt64(_root, "int", -999);
    Assert.Equal(-999, _doc.GetInt64(_root, "int"));

    _doc.PutUInt64(_root, "uint", 12345UL);
    Assert.Equal(12345UL, _doc.GetUInt64(_root, "uint"));

    _doc.PutDouble(_root, "dbl", -2.5);
    Assert.Equal(-2.5, _doc.GetDouble(_root, "dbl"));

    _doc.PutBoolean(_root, "bool", false);
    Assert.False(_doc.GetBoolean(_root, "bool"));

    byte[] bytes = [255, 0, 128];
    _doc.PutBytes(_root, "bytes", bytes);
    Assert.Equal(bytes, _doc.GetBytes(_root, "bytes"));
  }
}
