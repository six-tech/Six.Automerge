// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.TaskBoard;

/// <summary>
/// Event data emitted when the board changes.
/// </summary>
[PublicAPI]
public readonly record struct BoardChange(IReadOnlyList<Column> Columns, bool IsLocal);
