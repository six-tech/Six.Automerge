// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.TaskBoard;

/// <summary>
/// Represents a task card on the board.
/// </summary>
[PublicAPI]
public sealed class TaskItem
{
  /// <summary>
  /// The Automerge object ID for this task's map container.
  /// </summary>
  public required AutomergeObjectId ObjectId { get; init; }

  /// <summary>
  /// The task title.
  /// </summary>
  public required string Title { get; set; }

  /// <summary>
  /// A short description of the task.
  /// </summary>
  public required string Description { get; set; }

  /// <summary>
  /// The task status.
  /// </summary>
  public required string Status { get; set; }

  /// <summary>
  /// A vote counter for prioritization.
  /// </summary>
  public long Votes { get; set; }
}
