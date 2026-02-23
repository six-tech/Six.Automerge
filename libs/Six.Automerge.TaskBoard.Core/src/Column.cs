// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.TaskBoard;

/// <summary>
/// Represents a board column, backed by an Automerge map object.
/// </summary>
[PublicAPI]
public sealed class Column
{
  /// <summary>
  /// The Automerge object ID for this column's map container.
  /// </summary>
  public required AutomergeObjectId ObjectId { get; init; }

  /// <summary>
  /// The Automerge object ID for this column's task list container.
  /// </summary>
  public required AutomergeObjectId TasksListId { get; init; }

  /// <summary>
  /// The column display name.
  /// </summary>
  public required string Name { get; set; }

  /// <summary>
  /// Display order index for sorting columns.
  /// </summary>
  public long Order { get; set; }

  /// <summary>
  /// Tasks within this column.
  /// </summary>
  public List<TaskItem> Tasks { get; } = [];
}
