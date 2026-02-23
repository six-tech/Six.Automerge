// Copyright (c) Six. All rights reserved.

namespace DemoApp.Shared;

/// <summary>
/// Configuration options for <see cref="RepoService"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed record RepoServiceOptions
{
  /// <summary>
  /// Gets the app-specific hub path (e.g., "/hubs/text-editor", "/hubs/drawing").
  /// Combined with the resolved server base URL to produce the full hub URL.
  /// </summary>
  public required string HubPath { get; init; }

  /// <summary>
  /// Gets the full override URL of the SignalR hub.
  /// When set, this takes priority over automatic URL resolution from Aspire service discovery.
  /// When null, the URL is resolved from environment variables + <see cref="HubPath"/>.
  /// </summary>
  public string? HubUrl { get; init; }

  /// <summary>
  /// Gets the application name used for storage directory naming.
  /// </summary>
  public required string AppName { get; init; }

  /// <summary>
  /// Gets the base directory for file system storage.
  /// When null, defaults to <c>~/.rayautomerge/{AppName}/</c>.
  /// </summary>
  public string? StorageBaseDir { get; init; }

  /// <summary>
  /// Gets the connection timeout for the SignalR hub.
  /// Defaults to 10 seconds.
  /// </summary>
  public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
