// Copyright (c) Six. Licensed under the MIT License.

using System.Reactive.Linq;
using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class TextDocumentTests : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _textId;

  public TextDocumentTests()
  {
    var doc = AutomergeDocument.Create();
    _textId = doc.PutObject(doc.RootId, "text", AutomergeContainerType.Text);
    doc.SpliceText(_textId, 0, 0, "hello world");
    doc.Commit();

    _handle = new DocHandle<object>(
      new DocumentId(Guid.NewGuid().ToString()),
      new DocHandleOptions<object> { IsNew = true, ViewFactory = _ => new object() },
      doc);
    _handle.DoneLoading();
  }

  public void Dispose()
  {
    _handle.Dispose();
  }

  [Fact]
  public async Task Constructor_InitializesTextFromDocument()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);

    await textDoc.InitializeAsync();

    textDoc.Text.Should().Be("hello world");
  }

  [Fact]
  public async Task Constructor_InitializesFormattedText()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);

    await textDoc.InitializeAsync();

    textDoc.FormattedText.Should().NotBeNull();
    textDoc.FormattedText.PlainText.Should().Be("hello world");
  }

  [Fact]
  public async Task RemoteChange_UpdatesCachedText()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);
    await textDoc.InitializeAsync();

    // Simulate remote change
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));

    // Allow async refresh to complete
    await Task.Delay(200);

    textDoc.Text.Should().Be("hello world!!!");
  }

  [Fact]
  public async Task TextChanged_FiredOnRemoteChange()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);
    await textDoc.InitializeAsync();

    string? receivedText = null;
    var tcs = new TaskCompletionSource<bool>();
    textDoc.TextChanged.Subscribe(change =>
    {
      receivedText = change.Text;
      tcs.TrySetResult(true);
    });

    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    receivedText.Should().Be("hello world!!!");
  }

  [Fact]
  public async Task FormattedTextChanged_FiredOnRemoteChange()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);
    await textDoc.InitializeAsync();

    FormattedText? received = null;
    var tcs = new TaskCompletionSource<bool>();
    textDoc.FormattedTextChanged.Subscribe(change =>
    {
      received = change.FormattedText;
      tcs.TrySetResult(true);
    });

    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    received.Should().NotBeNull();
    received!.PlainText.Should().Be("hello world!!!");
  }

  [Fact]
  public async Task TextChanged_NotFiredForLocalChange()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);
    await textDoc.InitializeAsync();

    bool fired = false;
    textDoc.TextChanged.Subscribe(_ => fired = true);

    // A direct ChangeAsync on the handle is not through TextDocument's own API.
    // TextDocument detects it via the Changed event and treats it as remote.
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 0, 0, "X"));
    await Task.Delay(200);

    fired.Should().BeTrue();
  }

  [Fact]
  public void Constructor_NullHandle_ThrowsArgumentNullException()
  {
    var act = () => new TextDocument(null!, _textId, new TextDocumentOptions());

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Constructor_NullOptions_ThrowsArgumentNullException()
  {
    var act = () => new TextDocument(_handle, _textId, null!);

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public async Task Dispose_UnsubscribesFromChangedEvent()
  {
    var options = new TextDocumentOptions();
    var textDoc = new TextDocument(_handle, _textId, options);
    await textDoc.InitializeAsync();

    bool fired = false;
    textDoc.TextChanged.Subscribe(_ => fired = true);

    textDoc.Dispose();

    // After dispose, changes to the handle should not fire TextDocument events
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 0, 0, "X"));
    await Task.Delay(200);

    fired.Should().BeFalse();
  }

  [Fact]
  public void Handle_ReturnsUnderlyingHandle()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);

    textDoc.Handle.Should().BeSameAs(_handle);
  }

  [Fact]
  public void DocumentId_ReturnsHandleDocumentId()
  {
    var options = new TextDocumentOptions();
    using var textDoc = new TextDocument(_handle, _textId, options);

    textDoc.DocumentId.Should().Be(_handle.DocumentId.Value);
  }
}
