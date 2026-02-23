// Copyright (c) Six. Licensed under the MIT License.

using System.Reactive.Linq;
using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class TextDocumentMarkTests : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _textId;
  readonly TextDocument _textDoc;

  public TextDocumentMarkTests()
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

    var options = new TextDocumentOptions();
    _textDoc = new TextDocument(_handle, _textId, options);
    _textDoc.InitializeAsync().GetAwaiter().GetResult();
  }

  public void Dispose()
  {
    _textDoc.Dispose();
    _handle.Dispose();
  }

  [Fact]
  public async Task ApplyMarkAsync_BoolMark_AppliesFormattingToDocument()
  {
    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    // Verify marks are in the underlying document
    var marks = await _handle.ReadAsync(doc => doc.GetMarks(_textId));
    marks.Should().ContainSingle(m => m.Name == "bold");
  }

  [Fact]
  public async Task ApplyMarkAsync_StringMark_AppliesFormattingToDocument()
  {
    await _textDoc.ApplyMarkAsync(0, 5, "href", "http://example.com");

    var marks = await _handle.ReadAsync(doc => doc.GetMarks(_textId));
    marks.Should().ContainSingle(m => m.Name == "href");
  }

  [Fact]
  public async Task ApplyMarkAsync_UpdatesFormattedText()
  {
    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    _textDoc.FormattedText.Spans.Should().Contain(s => s.Marks.ContainsKey("bold"));
  }

  [Fact]
  public async Task ApplyMarkAsync_FormattedTextChangedEvent_Fires()
  {
    FormattedText? received = null;
    var tcs = new TaskCompletionSource<bool>();
    _textDoc.FormattedTextChanged.Subscribe(change =>
    {
      received = change.FormattedText;
      tcs.TrySetResult(true);
    });

    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    received.Should().NotBeNull();
    received!.Spans.Should().Contain(s => s.Marks.ContainsKey("bold"));
  }

  [Fact]
  public async Task ApplyMarkAsync_FormattedTextChangedEvent_IsLocal()
  {
    bool? isLocal = null;
    var tcs = new TaskCompletionSource<bool>();
    _textDoc.FormattedTextChanged.Subscribe(change =>
    {
      isLocal = change.IsLocal;
      tcs.TrySetResult(true);
    });

    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    isLocal.Should().BeTrue();
  }

  [Fact]
  public async Task RemoveMarkAsync_RemovesPreviouslyAppliedMark()
  {
    // First apply a mark
    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    var marksBefore = await _handle.ReadAsync(doc => doc.GetMarks(_textId));
    marksBefore.Should().ContainSingle(m => m.Name == "bold");

    // Now remove it
    await _textDoc.RemoveMarkAsync("bold", 0, 5, ExpandMode.Both);

    var marksAfter = await _handle.ReadAsync(doc => doc.GetMarks(_textId));
    marksAfter.Should().NotContain(m => m.Name == "bold");
  }

  [Fact]
  public async Task RemoveMarkAsync_FormattedTextChangedEvent_Fires()
  {
    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);

    FormattedText? received = null;
    var tcs = new TaskCompletionSource<bool>();
    _textDoc.FormattedTextChanged.Subscribe(change =>
    {
      received = change.FormattedText;
      tcs.TrySetResult(true);
    });

    await _textDoc.RemoveMarkAsync("bold", 0, 5, ExpandMode.Both);

    await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

    received.Should().NotBeNull();
    received!.Spans.Should().NotContain(s => s.Marks.ContainsKey("bold"));
  }

  [Fact]
  public async Task ApplyMarkAsync_FlushPendingTextChangesFirst()
  {
    // Schedule a text change that hasn't flushed yet
    await _textDoc.ApplyTextChangeAsync("hello bold world");

    // Wait for debounce to flush the text change
    await Task.Delay(200);

    // Apply a mark on the updated text
    await _textDoc.ApplyMarkAsync(6, 10, "bold", true, ExpandMode.Both);

    // Text should be updated
    _textDoc.Text.Should().Be("hello bold world");

    // Mark should be applied
    var marks = await _handle.ReadAsync(doc => doc.GetMarks(_textId));
    marks.Should().ContainSingle(m => m.Name == "bold");
  }

  [Fact]
  public async Task ApplyMarkAsync_MultipleMarks_AllPersist()
  {
    await _textDoc.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);
    await _textDoc.ApplyMarkAsync(0, 5, "italic", true, ExpandMode.Both);

    var marks = await _handle.ReadAsync(doc => doc.GetMarks(_textId));
    marks.Should().Contain(m => m.Name == "bold");
    marks.Should().Contain(m => m.Name == "italic");
  }

  [Fact]
  public void ApplyMarkAsync_AfterDispose_ThrowsObjectDisposedException()
  {
    _textDoc.Dispose();

    var act = async () => await _textDoc.ApplyMarkAsync(0, 5, "bold", true);

    act.Should().ThrowAsync<ObjectDisposedException>();
  }

  [Fact]
  public void RemoveMarkAsync_AfterDispose_ThrowsObjectDisposedException()
  {
    _textDoc.Dispose();

    var act = async () => await _textDoc.RemoveMarkAsync("bold", 0, 5);

    act.Should().ThrowAsync<ObjectDisposedException>();
  }
}
