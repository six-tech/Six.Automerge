using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Six.Automerge.Repo.Network.SignalR.Client;

/// <summary>
/// Represents the options for configuring a SignalR hub connection with the <see cref="SignalRConnectionService"/>
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public record SignalRConnectionServiceOptions
{
  /// <summary>
  /// Gets or sets the URL of the SignalR hub.
  /// </summary>
  public string HubUrl { get; set; } = string.Empty;

  /// <summary>
  /// Gets or sets a value indicating whether the SignalR connection should automatically start.
  /// </summary>
  /// <value>
  /// <c>true</c> if the SignalR connection should automatically start; otherwise, <c>false</c>.
  /// </value>
  public bool AutoStart { get; set; } = true;

  /// <summary>
  /// Gets or sets a value indicating whether automatic reconnection is enabled.
  /// </summary>
  /// <remarks>
  /// When automatic reconnection is enabled, the SignalR connection will attempt to reconnect automatically
  /// if the connection is lost. This is useful in scenarios where the network connection is unreliable.
  /// By default, automatic reconnection is enabled.
  /// </remarks>
  public bool EnableAutomaticReconnect { get; set; } = true;
  
  public bool UseMessagePackProtocol { get; set; } = true;
}