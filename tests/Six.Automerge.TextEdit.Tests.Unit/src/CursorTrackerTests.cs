// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class CursorTrackerTests : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly AutomergeObjectId _textId;
  readonly CursorTracker _tracker = new();

  public CursorTrackerTests()
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
  public async Task SaveAsync_ReturnsNonNullCursorState()
  {
    var state = await _tracker.SaveAsync(_handle, _textId, 5);

    state.Should().NotBeNull();
    state.CursorBytes.Should().NotBeEmpty();
  }

  [Fact]
  public async Task RestoreAsync_ReturnsSavedPosition()
  {
    var state = await _tracker.SaveAsync(_handle, _textId, 5);
    int restored = await _tracker.RestoreAsync(_handle, _textId, state);

    restored.Should().Be(5);
  }

  [Fact]
  public async Task RestoreAsync_AfterInsertBefore_ShiftsPositionForward()
  {
    // Save cursor at position 5 (between "hello" and " world")
    var state = await _tracker.SaveAsync(_handle, _textId, 5);

    // Insert text before the cursor position
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 0, 0, "XXX"));

    // Cursor should now be at 8 (5 + 3 inserted chars)
    int restored = await _tracker.RestoreAsync(_handle, _textId, state);

    restored.Should().Be(8);
  }

  [Fact]
  public async Task RestoreAsync_AfterInsertAfter_KeepsPosition()
  {
    // Save cursor at position 5
    var state = await _tracker.SaveAsync(_handle, _textId, 5);

    // Insert text after the cursor position
    await _handle.ChangeAsync(doc => doc.SpliceText(_textId, 8, 0, "YYY"));

    // Cursor should still be at 5
    int restored = await _tracker.RestoreAsync(_handle, _textId, state);

    restored.Should().Be(5);
  }

  [Fact]
  public async Task SaveAsync_AtBeginning_Works()
  {
    var state = await _tracker.SaveAsync(_handle, _textId, 0);
    int restored = await _tracker.RestoreAsync(_handle, _textId, state);

    restored.Should().Be(0);
  }

  [Fact]
  public async Task SaveAsync_AtLastChar_Works()
  {
    // "hello world" = 11 chars, last valid cursor index is 10
    var state = await _tracker.SaveAsync(_handle, _textId, 10);
    int restored = await _tracker.RestoreAsync(_handle, _textId, state);

    restored.Should().Be(10);
  }

  [Fact]
  public async Task CursorState_RoundtripSerialization()
  {
    var state = await _tracker.SaveAsync(_handle, _textId, 5);

    // Serialize and deserialize
    byte[] bytes = state.CursorBytes;
    var restored = new CursorState(bytes);

    int position = await _tracker.RestoreAsync(_handle, _textId, restored);
    position.Should().Be(5);
  }
}
