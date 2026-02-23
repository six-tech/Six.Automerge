// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

namespace Six.Automerge.SyncServer;

/// <summary>
/// Event args raised when a new document is first seen by the server.
/// </summary>
[PublicAPI]
public sealed class DocumentAvailableEventArgs : EventArgs
{
  /// <summary>
  /// The document ID that became available.
  /// </summary>
  public required string DocumentId { get; init; }
}
