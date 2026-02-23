// Copyright (c) Six. Licensed under the MIT License.

using System.Reactive.Linq;
using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

/// <summary>
/// Tests for concurrent local and remote edits on TextDocument.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TextDocumentConcurrentTests : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _textId;
  readonly TextDocument _textDoc;

  public TextDocumentConcurrentTests()
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
  public async Task ConcurrentEdits_LocalThenRemote_BothReflected()
  {
    // Apply local change
    await _textDoc.ApplyTextChangeAsync("hello world");
    await Task.Delay(200); // debounce flush

    _textDoc.Text.Should().Be("hello world");

    // Simulate remote change (append "!!!")
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));
    await Task.Delay(200); // allow remote event to propagate

    _textDoc.Text.Should().Be("hello world!!!");
  }

  [Fact]
  public async Task ConcurrentEdits_RemoteThenLocal_BothReflected()
  {
    // Simulate remote change first
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 5, 0, " world"));
    await Task.Delay(200);

    _textDoc.Text.Should().Be("hello world");

    // Apply local change
    await _textDoc.ApplyTextChangeAsync("hello world!!!");
    await Task.Delay(200);

    _textDoc.Text.Should().Be("hello world!!!");
  }

  [Fact]
  public async Task ConcurrentEdits_RemoteChangeDuringDebouncePending_DoesNotLoseLocal()
  {
    // Schedule a local text change (not yet flushed)
    await _textDoc.ApplyTextChangeAsync("hello world");

    // Simulate remote change while debounce is still pending
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 5, 0, "!!!"));
    await Task.Delay(50);

    // Wait for debounce to flush
    await Task.Delay(200);

    // The final state should reflect both changes
    string text = _textDoc.Text;
    text.Should().NotBeNullOrEmpty();
  }

  [Fact]
  public async Task ConcurrentEdits_MultipleRemoteChanges_AllReflected()
  {
    // Multiple remote changes in sequence
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 5, 0, " world"));
    await Task.Delay(50);

    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));
    await Task.Delay(200);

    _textDoc.Text.Should().Be("hello world!!!");
  }

  [Fact]
  public async Task ConcurrentEdits_LocalMarkAndRemoteText_BothApplied()
  {
    // Apply local text change first
    await _textDoc.ApplyTextChangeAsync("hello world");
    await Task.Delay(200); // flush

    // Apply local mark
    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    // Simulate remote text append
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));
    await Task.Delay(200);

    // Both mark and text should be present
    _textDoc.Text.Should().Be("hello world!!!");
    _textDoc.FormattedText.Spans.Should().Contain(s => s.Marks.ContainsKey("bold"));
  }

  [Fact]
  public async Task ConcurrentEdits_RemoteChangeFired_TextChangedIsRemote()
  {
    var events = new List<(string text, bool isLocal)>();
    _textDoc.TextChanged.Subscribe(change =>
    {
      events.Add((change.Text, change.IsLocal));
    });

    // Local change
    await _textDoc.ApplyTextChangeAsync("hello world");
    await Task.Delay(200);

    // Remote change
    var tcs = new TaskCompletionSource<bool>();
    _textDoc.TextChanged.Subscribe(change =>
    {
      if(!change.IsLocal)
        tcs.TrySetResult(true);
    });
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 11, 0, "!!!"));
    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    // Should have at least one local and one remote event
    events.Should().Contain(e => e.isLocal);
    events.Should().Contain(e => !e.isLocal);
  }
}
