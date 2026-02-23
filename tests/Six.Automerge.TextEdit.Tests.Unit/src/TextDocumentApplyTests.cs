// Copyright (c) Six. Licensed under the MIT License.

using System.Reactive.Linq;
using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class TextDocumentApplyTests : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _textId;
  readonly TextDocument _textDoc;

  public TextDocumentApplyTests()
  {
    var doc = AutomergeDocument.Create();
    _textId = doc.PutObject(doc.RootId, "text", AutomergeContainerType.Text);
    doc.SpliceText(_textId, 0, 0, "hello");
    doc.Commit();

    _handle = new DocHandle<object>(
      new DocumentId(Guid.NewGuid().ToString()),
      new DocHandleOptions<object> { IsNew = true, ViewFactory = _ => new object() },
      doc);
    _handle.DoneLoading();

    var options = new TextDocumentOptions { ChangeDebounceMs = 30 };
    _textDoc = new TextDocument(_handle, _textId, options);
    _textDoc.InitializeAsync().GetAwaiter().GetResult();
  }

  public void Dispose()
  {
    _textDoc.Dispose();
    _handle.Dispose();
  }

  [Fact]
  public async Task ApplyTextChangeAsync_AppendsText()
  {
    await _textDoc.ApplyTextChangeAsync("hello world");

    // Wait for debounce flush
    await Task.Delay(200);

    _textDoc.Text.Should().Be("hello world");

    // Verify the change persists in the actual document
    var actualText = await _handle.ReadAsync(doc => doc.GetText(_textId));
    actualText.Should().Be("hello world");
  }

  [Fact]
  public async Task ApplyTextChangeAsync_DeletesText()
  {
    await _textDoc.ApplyTextChangeAsync("hel");

    await Task.Delay(200);

    _textDoc.Text.Should().Be("hel");
  }

  [Fact]
  public async Task ApplyTextChangeAsync_ReplacesText()
  {
    await _textDoc.ApplyTextChangeAsync("world");

    await Task.Delay(200);

    _textDoc.Text.Should().Be("world");
  }

  [Fact]
  public async Task ApplyTextChangeAsync_RapidChanges_OnlyAppliesFinal()
  {
    await _textDoc.ApplyTextChangeAsync("h");
    await _textDoc.ApplyTextChangeAsync("he");
    await _textDoc.ApplyTextChangeAsync("hel");
    await _textDoc.ApplyTextChangeAsync("hell");
    await _textDoc.ApplyTextChangeAsync("hello world");

    await Task.Delay(200);

    _textDoc.Text.Should().Be("hello world");
  }

  [Fact]
  public async Task ApplyTextChangeAsync_TextChangedEvent_Fires()
  {
    string? eventText = null;
    var tcs = new TaskCompletionSource<bool>();
    _textDoc.TextChanged.Subscribe(change =>
    {
      eventText = change.Text;
      tcs.TrySetResult(true);
    });

    await _textDoc.ApplyTextChangeAsync("hello world");

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    eventText.Should().Be("hello world");
  }

  [Fact]
  public async Task ApplyTextChangeAsync_TextChangedEvent_IsLocal()
  {
    bool? isLocal = null;
    var tcs = new TaskCompletionSource<bool>();
    _textDoc.TextChanged.Subscribe(change =>
    {
      isLocal = change.IsLocal;
      tcs.TrySetResult(true);
    });

    await _textDoc.ApplyTextChangeAsync("hello world");

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    isLocal.Should().BeTrue();
  }

  [Fact]
  public async Task ApplyTextChangeAsync_NoChange_DoesNothing()
  {
    bool fired = false;
    _textDoc.TextChanged.Subscribe(_ => fired = true);

    await _textDoc.ApplyTextChangeAsync("hello");

    await Task.Delay(200);

    fired.Should().BeFalse();
    _textDoc.Text.Should().Be("hello");
  }

  [Fact]
  public void ApplyTextChangeAsync_AfterDispose_ThrowsObjectDisposedException()
  {
    _textDoc.Dispose();

    var act = async () => await _textDoc.ApplyTextChangeAsync("test");

    act.Should().ThrowAsync<ObjectDisposedException>();
  }

  [Fact]
  public async Task ApplyTextChangeAsync_EmptyString_ClearsText()
  {
    await _textDoc.ApplyTextChangeAsync("");

    await Task.Delay(200);

    _textDoc.Text.Should().BeEmpty();
  }
}
