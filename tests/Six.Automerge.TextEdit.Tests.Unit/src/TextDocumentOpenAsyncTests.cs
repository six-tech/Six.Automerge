// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class TextDocumentOpenAsyncTests : IDisposable
{
  readonly Repository.Repo _repo;

  public TextDocumentOpenAsyncTests()
  {
    // Use InMemoryNetworkAdapter so Repo.Find() doesn't hang when doc is missing
    var adapter = new InMemoryNetworkAdapter();
    _repo = new Repository.Repo(new RepoConfig
    {
      NetworkAdapters = [adapter]
    });
  }

  public void Dispose()
  {
    _repo.Dispose();
  }

  [Fact]
  public async Task OpenAsync_ExistingDocument_ReturnsTextDocument()
  {
    // Create a document first
    using var created = await TextDocument.CreateAsync(_repo);
    string docId = created.DocumentId;

    // Open the same document (already in cache)
    using var opened = await TextDocument.OpenAsync(_repo, docId);

    opened.Should().NotBeNull();
    opened!.DocumentId.Should().Be(docId);
  }

  [Fact]
  public async Task OpenAsync_ExistingDocument_ReadsExistingText()
  {
    // Create a document and write some text
    using var created = await TextDocument.CreateAsync(_repo);
    await created.ApplyTextChangeAsync("hello world");
    await Task.Delay(100); // wait for debounce flush

    string docId = created.DocumentId;

    // Open the same document
    using var opened = await TextDocument.OpenAsync(_repo, docId);

    opened.Should().NotBeNull();
    opened!.Text.Should().Be("hello world");
  }

  [Fact]
  public async Task OpenAsync_NonExistentDocument_ReturnsNull()
  {
    var result = await TextDocument.OpenAsync(
      _repo,
      DocumentId.Generate(),
      timeout: TimeSpan.FromMilliseconds(200));

    result.Should().BeNull();
  }

  [Fact]
  public async Task OpenAsync_CustomTextFieldKey_ReadsCorrectField()
  {
    var options = new TextDocumentOptions { TextFieldKey = "body" };
    using var created = await TextDocument.CreateAsync(_repo, options);
    await created.ApplyTextChangeAsync("custom key text");
    await Task.Delay(100);

    string docId = created.DocumentId;

    using var opened = await TextDocument.OpenAsync(_repo, docId, options);

    opened.Should().NotBeNull();
    opened!.Text.Should().Be("custom key text");
  }

  [Fact]
  public async Task OpenAsync_WrongTextFieldKey_ReturnsNull()
  {
    // Create with default "content" key
    using var created = await TextDocument.CreateAsync(_repo);
    string docId = created.DocumentId;

    // Try to open with a different key that doesn't exist
    var options = new TextDocumentOptions { TextFieldKey = "nonexistent" };
    var result = await TextDocument.OpenAsync(_repo, docId, options);

    result.Should().BeNull();
  }

  [Fact]
  public async Task OpenAsync_NullRepo_ThrowsArgumentNullException()
  {
    Func<Task> act = async () => await TextDocument.OpenAsync(null!, "some-id");

    await act.Should().ThrowAsync<ArgumentNullException>();
  }

  [Fact]
  public async Task OpenAsync_NullDocumentId_ThrowsArgumentException()
  {
    Func<Task> act = async () => await TextDocument.OpenAsync(_repo, null!);

    await act.Should().ThrowAsync<ArgumentException>();
  }

  [Fact]
  public async Task OpenAsync_EmptyDocumentId_ThrowsArgumentException()
  {
    Func<Task> act = async () => await TextDocument.OpenAsync(_repo, "");

    await act.Should().ThrowAsync<ArgumentException>();
  }
}
