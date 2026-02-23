// Copyright (c) Six. All rights reserved.

namespace Six.Automerge.Repo.Network.SignalR;

/// <summary>
/// Event arguments for when a new document becomes available on the server.
/// </summary>
[PublicAPI]
public sealed class DocumentAvailableEventArgs : EventArgs
{
  /// <summary>
  /// Gets the ID of the newly available document.
  /// </summary>
  public required string DocumentId { get; init; }
}
