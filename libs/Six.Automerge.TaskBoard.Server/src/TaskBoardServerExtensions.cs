// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.TaskBoard.Server;

/// <summary>
/// Constants and helpers for the task board sync server.
/// </summary>
[PublicAPI]
public static class TaskBoardServerExtensions
{
  /// <summary>
  /// The SignalR hub path for the collaborative task board.
  /// </summary>
  public const string HubPath = "/hubs/task-board";
}
