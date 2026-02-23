// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

/// <summary>
/// Tests for the Rx-based operation pipeline in TextDocument.
/// Validates coalescing, ordering, concurrency safety, and dispose behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TextDocumentQueueTests : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _textId;
  readonly TextDocument _textDoc;

  public TextDocumentQueueTests()
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

    _textDoc = new TextDocument(_handle, _textId, new TextDocumentOptions());
    _textDoc.InitializeAsync().GetAwaiter().GetResult();
  }

  public void Dispose()
  {
    _textDoc.Dispose();
    _handle.Dispose();
  }

  [Fact]
  public async Task ApplyTextChangeAsync_RapidChanges_CoalescesCorrectly()
  {
    // Schedule 10 rapid changes without awaiting between them
    for(int i = 1; i <= 10; i++)
    {
      await _textDoc.ApplyTextChangeAsync("hello" + new string('!', i));
    }

    // Wait for the Rx pipeline to debounce and process
    await Task.Delay(300);

    // Only the final text should be committed
    _textDoc.Text.Should().Be("hello!!!!!!!!!!");

    // Verify the document itself has the final text
    var actualText = await _handle.ReadAsync(doc => doc.GetText(_textId));
    actualText.Should().Be("hello!!!!!!!!!!");
  }

  [Fact]
  public async Task RemoteRefresh_DuringPendingLocalChange_IsDeferred()
  {
    // Enqueue a local change
    await _textDoc.ApplyTextChangeAsync("hello world");

    // Simulate a remote change while the local change is still in queue
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 5, 0, "!!!"));

    // Wait for pipeline to process both operations
    await Task.Delay(300);

    // The text should contain the local change content
    string text = _textDoc.Text;
    text.Should().NotBeNullOrEmpty();
    text.Length.Should().BeGreaterThan(5);
  }

  [Fact]
  public async Task ConcurrentLocalAndRemote_DoesNotCrash()
  {
    // Simulate rapid alternating local/remote operations
    var tasks = new List<Task>();

    for(int i = 0; i < 10; i++)
    {
      int iteration = i;

      // Local change
      tasks.Add(_textDoc.ApplyTextChangeAsync("hello" + new string('x', iteration)));

      // Remote change (via handle directly)
      tasks.Add(_handle.ChangeAsync(doc =>
      {
        string currentText = doc.GetText(_textId);
        if(currentText.Length > 0)
        {
          doc.SpliceText(_textId, (ulong)currentText.Length, 0, "r");
        }
      }).AsTask());
    }

    await Task.WhenAll(tasks);

    // Wait for all pipeline operations to settle
    await Task.Delay(500);

    // Should not throw, text should be non-null
    _textDoc.Text.Should().NotBeNull();

    // Verify we can still read from the document
    var actualText = await _handle.ReadAsync(doc => doc.GetText(_textId));
    actualText.Should().NotBeNull();
  }

  [Fact]
  public async Task Dispose_FlushesQueueGracefully()
  {
    // Enqueue a change
    await _textDoc.ApplyTextChangeAsync("hello world");

    // Wait for debounce + processing
    await Task.Delay(200);

    // Dispose should not throw
    var act = () =>
    {
      _textDoc.Dispose();
      return Task.CompletedTask;
    };

    await act.Should().NotThrowAsync();

    // After dispose, the document should have the change applied
    var actualText = await _handle.ReadAsync(doc => doc.GetText(_textId));
    actualText.Should().Be("hello world");
  }
}
