using Six.Automerge.Repository;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit;

[Trait("Category", "Core")]
public sealed class AutomergeUrlTests
{
  [Fact]
  public void Generate_ShouldCreateValidUrl()
  {
    // Act
    var url = AutomergeUrl.Generate();

    // Assert
    Assert.NotNull(url);
    Assert.StartsWith("automerge:", url);
    Assert.True(AutomergeUrl.IsValid(url));
  }

  [Fact]
  public void Parse_WithValidUrl_ShouldExtractDocumentId()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var url = $"automerge:{documentId}";

    // Act
    var parsed = AutomergeUrl.Parse(url);

    // Assert
    Assert.Equal(documentId, parsed.DocumentId);
    Assert.Null(parsed.Heads);
  }

  [Fact]
  public void Parse_WithHeads_ShouldExtractDocumentIdAndHeads()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var heads = new[] { "head1", "head2", "head3" };
    var url = $"automerge:{documentId}#{string.Join("|", heads)}";

    // Act
    var parsed = AutomergeUrl.Parse(url);

    // Assert
    Assert.Equal(documentId, parsed.DocumentId);
    Assert.NotNull(parsed.Heads);
    Assert.Equal(3, parsed.Heads.Count);
    Assert.Equal(heads, parsed.Heads);
  }

  [Fact]
  public void Parse_WithSingleHead_ShouldExtractHead()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var url = $"automerge:{documentId}#singleHead";

    // Act
    var parsed = AutomergeUrl.Parse(url);

    // Assert
    Assert.NotNull(parsed.Heads);
    Assert.Single(parsed.Heads);
    Assert.Equal("singleHead", parsed.Heads[0]);
  }

  [Fact]
  public void Parse_WithEmptyHeads_ShouldReturnNullHeads()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var url = $"automerge:{documentId}#";

    // Act
    var parsed = AutomergeUrl.Parse(url);

    // Assert
    Assert.Null(parsed.Heads);
  }

  [Fact]
  public void Format_WithDocumentIdOnly_ShouldCreateValidUrl()
  {
    // Arrange
    var documentId = DocumentId.Generate();

    // Act
    var url = AutomergeUrl.Format(documentId);

    // Assert
    Assert.Equal($"automerge:{documentId}", url);
    Assert.True(AutomergeUrl.IsValid(url));
  }

  [Fact]
  public void Format_WithHeads_ShouldIncludeHeadsInUrl()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var heads = new[] { "head1", "head2" };

    // Act
    var url = AutomergeUrl.Format(documentId, heads);

    // Assert
    Assert.Equal($"automerge:{documentId}#head1|head2", url);
    Assert.True(AutomergeUrl.IsValid(url));
  }

  [Fact]
  public void Format_WithEmptyHeadsList_ShouldNotIncludeHash()
  {
    // Arrange
    var documentId = DocumentId.Generate();

    // Act
    var url = AutomergeUrl.Format(documentId, Array.Empty<string>());

    // Assert
    Assert.Equal($"automerge:{documentId}", url);
    Assert.DoesNotContain("#", url);
  }

  [Fact]
  public void RoundTrip_Parse_AndFormat_ShouldBeEqual()
  {
    // Arrange
    var originalUrl = AutomergeUrl.Generate();

    // Act
    var parsed = AutomergeUrl.Parse(originalUrl);
    var reconstructedUrl = AutomergeUrl.Format(parsed.DocumentId, parsed.Heads);

    // Assert
    Assert.Equal(originalUrl, reconstructedUrl);
  }

  [Fact]
  public void RoundTrip_WithHeads_ShouldPreserveHeads()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var heads = new[] { "abc", "def", "ghi" };
    var originalUrl = AutomergeUrl.Format(documentId, heads);

    // Act
    var parsed = AutomergeUrl.Parse(originalUrl);

    // Assert
    Assert.Equal(heads, parsed.Heads);
  }

  [Fact]
  public void GetDocumentId_ShouldExtractId()
  {
    // Arrange
    var documentId = DocumentId.Generate();
    var url = AutomergeUrl.Format(documentId, new[] { "head1" });

    // Act
    var extractedId = AutomergeUrl.GetDocumentId(url);

    // Assert
    Assert.Equal(documentId, extractedId);
  }

  [Fact]
  public void IsValid_WithValidUrl_ShouldReturnTrue()
  {
    // Arrange
    var url = AutomergeUrl.Generate();

    // Act
    var isValid = AutomergeUrl.IsValid(url);

    // Assert
    Assert.True(isValid);
  }

  [Fact]
  public void IsValid_WithInvalidScheme_ShouldReturnFalse()
  {
    // Arrange
    var url = "invalid:" + DocumentId.Generate();

    // Act
    var isValid = AutomergeUrl.IsValid(url);

    // Assert
    Assert.False(isValid);
  }

  [Fact]
  public void IsValid_WithNoDocumentId_ShouldReturnFalse()
  {
    // Arrange
    var url = "automerge:";

    // Act
    var isValid = AutomergeUrl.IsValid(url);

    // Assert
    Assert.False(isValid);
  }

  [Fact]
  public void Parse_WithNull_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => AutomergeUrl.Parse(null!));
  }

  [Fact]
  public void Parse_WithEmpty_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => AutomergeUrl.Parse(string.Empty));
  }

  [Fact]
  public void Parse_WithoutScheme_ShouldThrow()
  {
    // Arrange
    var url = DocumentId.Generate();

    // Act & Assert
    Assert.Throws<FormatException>(() => AutomergeUrl.Parse(url));
  }

  [Fact]
  public void Parse_WithInvalidDocumentId_ShouldThrow()
  {
    // Arrange
    var url = "automerge:invalid-id";

    // Act & Assert
    Assert.Throws<FormatException>(() => AutomergeUrl.Parse(url));
  }

  [Fact]
  public void Format_WithNull_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => AutomergeUrl.Format((string)null!));
  }

  [Fact]
  public void Format_WithEmpty_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<ArgumentException>(() => AutomergeUrl.Format(string.Empty));
  }

  [Fact]
  public void Format_WithInvalidDocumentId_ShouldThrow()
  {
    // Act & Assert
    Assert.Throws<FormatException>(() => AutomergeUrl.Format("invalid-id"));
  }
}
