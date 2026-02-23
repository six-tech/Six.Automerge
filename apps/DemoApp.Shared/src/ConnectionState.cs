// Copyright (c) Six. All rights reserved.

namespace DemoApp.Shared;

/// <summary>
/// Represents the connection state of the RepoService.
/// </summary>
[PublicAPI]
public enum ConnectionState
{
  /// <summary>Not connected to the server.</summary>
  Disconnected,

  /// <summary>Attempting to connect to the server.</summary>
  Connecting,

  /// <summary>Connected to the server and syncing.</summary>
  Connected,

  /// <summary>Connection was lost, attempting to reconnect.</summary>
  Reconnecting
}
