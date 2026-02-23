// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class TextDocumentCreateAsyncTests : IDisposable
{
  readonly Repository.Repo _repo;

  public TextDocumentCreateAsyncTests()
  {
    _repo = new Repository.Repo(new RepoConfig());
  }

  public void Dispose()
  {
    _repo.Dispose();
  }

  [Fact]
  public async Task CreateAsync_ReturnsInitializedTextDocument()
  {
    using var textDoc = await TextDocument.CreateAsync(_repo);

    textDoc.Should().NotBeNull();
    textDoc.Text.Should().BeEmpty();
    textDoc.FormattedText.PlainText.Should().BeEmpty();
  }

  [Fact]
  public async Task CreateAsync_ExposesDocumentId()
  {
    using var textDoc = await TextDocument.CreateAsync(_repo);

    textDoc.DocumentId.Should().NotBeNullOrEmpty();
  }

  [Fact]
  public async Task CreateAsync_ExposesHandle()
  {
    using var textDoc = await TextDocument.CreateAsync(_repo);

    textDoc.Handle.Should().NotBeNull();
    textDoc.Handle.IsReady.Should().BeTrue();
  }

  [Fact]
  public async Task CreateAsync_DefaultOptions_UsesContentKey()
  {
    using var textDoc = await TextDocument.CreateAsync(_repo);

    // Verify the text field exists under "content" key
    var textObjectId = await textDoc.Handle.ReadAsync(
      doc => doc.GetObject(doc.RootId, "content"));

    textObjectId.Should().NotBeNull();
  }

  [Fact]
  public async Task CreateAsync_CustomTextFieldKey_UsesSpecifiedKey()
  {
    var options = new TextDocumentOptions { TextFieldKey = "body" };
    using var textDoc = await TextDocument.CreateAsync(_repo, options);

    // Verify the text field exists under custom key
    var textObjectId = await textDoc.Handle.ReadAsync(
      doc => doc.GetObject(doc.RootId, "body"));

    textObjectId.Should().NotBeNull();

    // Default key should not exist
    var defaultKey = await textDoc.Handle.ReadAsync(
      doc => doc.GetObject(doc.RootId, "content"));

    defaultKey.Should().BeNull();
  }

  [Fact]
  public async Task CreateAsync_TextChangesApply()
  {
    using var textDoc = await TextDocument.CreateAsync(_repo);

    await textDoc.ApplyTextChangeAsync("hello world");
    await Task.Delay(100); // debounce

    textDoc.Text.Should().Be("hello world");
  }

  [Fact]
  public async Task CreateAsync_NullRepo_ThrowsArgumentNullException()
  {
    Func<Task> act = async () => await TextDocument.CreateAsync(null!);

    await act.Should().ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task CreateAsync_NullOptions_UsesDefaults()
  {
    using var textDoc = await TextDocument.CreateAsync(_repo, null);

    textDoc.Should().NotBeNull();
    textDoc.Text.Should().BeEmpty();
  }
}
