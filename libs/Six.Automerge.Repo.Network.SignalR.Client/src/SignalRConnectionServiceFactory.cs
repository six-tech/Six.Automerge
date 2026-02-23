using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Six.Automerge.Repo.Network.SignalR.Client;

/// <summary>
/// Provides methods to create instances of SignalR connection services.
/// </summary>
internal static class SignalRConnectionServiceFactory
{
  /// <summary>
  /// Creates an instance of the specified signalR connection service.
  /// </summary>
  /// <param name="serviceProvider">The <see cref="IServiceProvider"/> instance to extend on.</param>
  /// <param name="name">The name of the signalR connection service.</param>
  /// <returns>An instance of the specified signalR connection service.</returns>
  public static SignalRConnectionService Create(IServiceProvider serviceProvider, string name)
  {
    var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<SignalRConnectionServiceOptions>>();
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

    return new SignalRConnectionService(loggerFactory, optionsMonitor, name);
  }
}