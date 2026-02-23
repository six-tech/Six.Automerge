// Copyright (c) Six. Licensed under the MIT License.

using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit;

/// <summary>
/// Serialised cursor position that survives concurrent edits.
/// </summary>
[PublicAPI]
public sealed class CursorState
{
  public byte[] CursorBytes { get; }

  public CursorState(byte[] cursorBytes)
  {
    ArgumentNullException.ThrowIfNull(cursorBytes);
    CursorBytes = cursorBytes;
  }
}

/// <summary>
/// Saves and restores cursor positions in Automerge text objects
/// using the native cursor API. Cursors track logical positions
/// that automatically adjust when concurrent edits shift indices.
/// </summary>
[PublicAPI]
public sealed class CursorTracker
{
  /// <summary>
  /// Saves the current cursor position as a stable <see cref="CursorState"/>.
  /// </summary>
  public async ValueTask<CursorState> SaveAsync(
    DocHandle<object> handle,
    AutomergeObjectId textObjectId,
    int position,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(handle);
    ArgumentNullException.ThrowIfNull(textObjectId);

    var cursorBytes = await handle.ReadAsync(
      doc =>
      {
        var cursor = doc.GetCursor(textObjectId, (ulong)position);
        return cursor.ToBytes();
      },
      cancellationToken).ConfigureAwait(false);

    return new CursorState(cursorBytes);
  }

  /// <summary>
  /// Restores a previously saved cursor position, accounting for any
  /// concurrent edits that occurred since the cursor was saved.
  /// </summary>
  public async ValueTask<int> RestoreAsync(
    DocHandle<object> handle,
    AutomergeObjectId textObjectId,
    CursorState state,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(handle);
    ArgumentNullException.ThrowIfNull(textObjectId);
    ArgumentNullException.ThrowIfNull(state);

    var position = await handle.ReadAsync(
      doc =>
      {
        var cursor = AutomergeCursor.FromBytes(state.CursorBytes);
        return (int)doc.GetCursorPosition(textObjectId, cursor);
      },
      cancellationToken).ConfigureAwait(false);

    return position;
  }
}
