using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Six.Automerge.Repo.Network.SignalR.Client.Extensions;

[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SignalRConnectionServiceExtensions
{
  /// <summary>
  /// Configures the host to use the default <see cref="SignalRConnectionService"/>
  /// implementation with the given name and options.
  /// </summary>
  /// <param name="hostBuilder">The <see cref="IHostBuilder"/> instance.</param>
  /// <param name="name">
  /// The name to be used as key (service name) for this signalR connection service. This name is used to
  /// inject the SignalR connection service into other services that depend on it.
  /// <br></br>
  /// <para>
  /// For example:
  /// <c>
  /// [FromKeyedServices("GameSimulatorHub")] ISignalRConnectionService connectionService
  /// </c>
  /// </para>
  /// <br></br>
  /// </param>
  /// <param name="configureOptions">An action to configure the <see cref="SignalRConnectionServiceOptions"/>.</param>
  /// <returns>The <see cref="IHostBuilder"/> instance.</returns>
  public static IHostBuilder AddSignalRConnectionService(
    this IHostBuilder hostBuilder,
    string name,
    Action<SignalRConnectionServiceOptions> configureOptions)
  {
    hostBuilder.ConfigureServices((_, services) =>
    {
      // Configure named options for SignalRConnectionServiceOptions
      services.Configure(name, configureOptions);

      // Register with the DI container as keyed service
      services.AddKeyedSingleton<ISignalRConnectionService, SignalRConnectionService>(name, (serviceProvider, _) =>
        SignalRConnectionServiceFactory.Create(serviceProvider, name));
    });

    return hostBuilder;
  }


  /// <summary>
  /// Configures the host to use the default <see cref="SignalRConnectionService"/>
  /// implementation with the given name and options (builder).
  /// </summary>
  /// <param name="hostBuilder">The host builder.</param>
  /// <param name="name">
  /// The name to be used as key (service) for this signalR connection service. This name is used to
  /// inject the SignalR connection service into other services that depend on it.
  /// <br></br>
  /// <para>
  /// For example:
  /// <c>
  /// [FromKeyedServices("GameSimulatorHub")] ISignalRConnectionService connectionService
  /// </c>
  /// </para>
  /// <br></br>
  /// </param>
  /// <param name="configureOptions">An action to configure the SignalR hub options.</param>
  /// <returns>The <see cref="IHostBuilder"/> instance.</returns>
  public static IHostBuilder AddSignalRConnectionService(
    this IHostBuilder hostBuilder,
    string name,
    Action<OptionsBuilder<SignalRConnectionServiceOptions>> configureOptions)
  {
    hostBuilder.ConfigureServices((_, services) =>
    {
      // Apply the configuration to named SignalRConnectionServiceOptions
      var optionsBuilder = services.AddOptions<SignalRConnectionServiceOptions>(name);
      configureOptions.Invoke(optionsBuilder);

      // Register with the DI container as keyed service
      services.AddKeyedSingleton<ISignalRConnectionService, SignalRConnectionService>(name, (serviceProvider, _) =>
        SignalRConnectionServiceFactory.Create(serviceProvider, name));
    });

    return hostBuilder;
  }


  /// <summary>
  /// Configures the host to use the default <see cref="SignalRConnectionService"/>
  /// implementation with the given name and options (builder).
  /// </summary>
  /// <param name="wasmHostBuilder">The WebAssemblyHostBuilder instance.</param>
  /// <param name="name">The name of the connection service.</param>
  /// <param name="configureOptions">An action to configure the SignalRConnectionServiceOptions.</param>
  /// <returns>The WebAssemblyHostBuilder instance.</returns>
  public static WebAssemblyHostBuilder AddSignalRConnectorService(
    this WebAssemblyHostBuilder wasmHostBuilder,
    string name,
    Action<SignalRConnectionServiceOptions> configureOptions)
  {
    // Configure named options for SignalRConnectionServiceOptions
    wasmHostBuilder.Services.Configure(name, configureOptions);
    
    wasmHostBuilder.Services.AddKeyedSingleton<ISignalRConnectionService, SignalRConnectionService>(name, (serviceProvider, _) =>
      SignalRConnectionServiceFactory.Create(serviceProvider, name));
  
    return wasmHostBuilder;
  }


  /// <summary>
  /// Configures the host to use the default <see cref="SignalRConnectionService"/>
  /// implementation with the given name and options (builder).
  /// </summary>
  /// <param name="wasmHostBuilder">The host builder.</param>
  /// <param name="name">The name of the SignalR connection provider.</param>
  /// <param name="configureOptions">Action to configure the SignalR hub options.</param>
  /// <returns>The host builder.</returns>
  public static WebAssemblyHostBuilder AddSignalRConnectionService(
    this WebAssemblyHostBuilder wasmHostBuilder,
    string name,
    Action<OptionsBuilder<SignalRConnectionServiceOptions>> configureOptions)
  {
    // Apply the configuration to named SignalRConnectionServiceOptions
    var optionsBuilder = wasmHostBuilder.Services.AddOptions<SignalRConnectionServiceOptions>(name);
    configureOptions.Invoke(optionsBuilder);
    
    wasmHostBuilder.Services.AddKeyedSingleton<ISignalRConnectionService, SignalRConnectionService>(name, (serviceProvider, _) =>
      SignalRConnectionServiceFactory.Create(serviceProvider, name));

    return wasmHostBuilder;
  }
}