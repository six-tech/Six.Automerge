using Six.Automerge.Repository;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit;

[Trait("Category", "Core")]
public sealed class DocumentIdTests
{
  [Fact]
  public void Generate_ShouldCreateValidId()
  {
    // Act
    var id = DocumentId.Generate();

    // Assert
    Assert.NotEqual(default, id);
    Assert.False(string.IsNullOrWhiteSpace(id.Value));
    Assert.True(DocumentId.IsValid(id));
  }

  [Fact]
  public void Generate_ShouldCreateUniqueIds()
  {
    // Act
    var id1 = DocumentId.Generate();
    var id2 = DocumentId.Generate();

    // Assert
    Assert.NotEqual(id1, id2);
  }

  [Fact]
  public void FromBinary_AndToBinary_ShouldRoundTrip()
  {
    // Arrange
    var originalData = Guid.NewGuid().ToByteArray();

    // Act
    var id = DocumentId.FromBinary(originalData);
    var decodedData = id.ToBinary();

    // Assert
    Assert.Equal(originalData, decodedData);
  }

  [Fact]
  public void ToBinary_AndFromBinary_ShouldRoundTrip()
  {
    // Arrange
    var originalId = DocumentId.Generate();

    // Act
    var binary = originalId.ToBinary();
    var reconstructedId = DocumentId.FromBinary(binary);

    // Assert
    Assert.Equal(originalId, reconstructedId);
  }

  [Fact]
  public void IsValid_WithValidId_ShouldReturnTrue()
  {
    // Arrange
    var id = DocumentId.Generate();

    // Act
    var isValid = DocumentId.IsValid(id);

    // Assert
    Assert.True(isValid);
  }

  [Fact]
  public void IsValid_WithNull_ShouldReturnFalse()
  {
    // Act
    var isValid = DocumentId.IsValid(null!);

    // Assert
    Assert.False(isValid);
  }

  [Fact]
  public void IsValid_WithEmpty_ShouldReturnFalse()
  {
    // Act
    var isValid = DocumentId.IsValid(string.Empty);

    // Assert
    Assert.False(isValid);
  }

  [Fact]
  public void IsValid_WithWhitespace_ShouldReturnFalse()
  {
    // Act
    var isValid = DocumentId.IsValid("   ");

    // Assert
    Assert.False(isValid);
  }

  [Fact]
  public void IsValid_WithInvalidBase58_ShouldReturnFalse()
  {
    // Act - Using characters not in Base58 alphabet (0, O, I, l)
    var isValid = DocumentId.IsValid("0OIl");

    // Assert
    Assert.False(isValid);
  }

  [Fact]
  public void FromBinary_WithTooShortData_ShouldThrow()
  {
    // Arrange
    var shortData = new byte[8];

    // Act & Assert
    Assert.Throws<ArgumentException>(() => DocumentId.FromBinary(shortData));
  }

  [Fact]
  public void FromBinary_WithNull_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentNullException>(() => DocumentId.FromBinary(null!));
  }

  [Fact]
  public void FromBinary_WithEmpty_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => DocumentId.FromBinary(Array.Empty<byte>()));
  }

  [Fact]
  public void ToBinary_WithNull_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => DocumentId.ToBinary((string)null!));
  }

  [Fact]
  public void ToBinary_WithEmpty_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => DocumentId.ToBinary(string.Empty));
  }

  [Fact]
  public void ToBinary_WithInvalidChecksum_ShouldThrow()
  {
    // Arrange - Generate valid ID, then corrupt it
    var validId = DocumentId.Generate();
    var corruptedId = validId.Value[..^1] + "X";

    // Act & Assert
    Assert.Throws<FormatException>(() => DocumentId.ToBinary(corruptedId));
  }

  [Fact]
  public void DocumentId_ShouldNotContainAmbiguousCharacters()
  {
    // Arrange
    var id = DocumentId.Generate();

    // Assert - Should not contain 0, O, I, l
    Assert.DoesNotContain('0', id.Value);
    Assert.DoesNotContain('O', id.Value);
    Assert.DoesNotContain('I', id.Value);
    Assert.DoesNotContain('l', id.Value);
  }

  [Fact]
  public void DocumentId_FromUuidBytes_ShouldBe16Bytes()
  {
    // Arrange
    var id = DocumentId.Generate();

    // Act
    var binary = id.ToBinary();

    // Assert
    Assert.Equal(16, binary.Length);
  }
}
