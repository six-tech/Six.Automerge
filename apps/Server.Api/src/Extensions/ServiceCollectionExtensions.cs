// Copyright (c) Six. All rights reserved.

using Microsoft.AspNetCore.SignalR;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.Repository.Storage.FileSystem;
using Six.Automerge.SyncServer;
using Server.Api.Hubs;
using Server.Api.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SyncServer infrastructure in the DI container.
/// </summary>
[PublicAPI]
public static class ServerApiServiceCollectionExtensions
{
  /// <summary>
  /// Registers <see cref="SyncServer"/>, <see cref="SignalRServerTransport"/>,
  /// and the hosted service for graceful shutdown.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <param name="configureStorage">
  /// Optional action to configure the storage directory.
  /// Defaults to <c>{AppContext.BaseDirectory}/documents</c>.
  /// </param>
  /// <returns>The service collection for chaining.</returns>
  public static IServiceCollection AddSyncServer(
    this IServiceCollection services,
    Action<SyncServerRegistrationOptions>? configureStorage = null)
  {
    ArgumentNullException.ThrowIfNull(services);

    var registrationOptions = new SyncServerRegistrationOptions();
    configureStorage?.Invoke(registrationOptions);

    // Register peer tracker singleton
    services.AddSingleton<DocumentPeerTracker>();

    // Register transport singleton
    services.AddSingleton<SignalRServerTransport>();

    // Register SyncServer singleton
    services.AddSingleton(sp =>
    {
      var transport = sp.GetRequiredService<SignalRServerTransport>();
      var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

      var storageDir = registrationOptions.StorageDirectory;
      Directory.CreateDirectory(storageDir);
      var storage = new FileSystemStorageAdapter(storageDir);

      var options = new SyncServerOptions
      {
        Storage = storage,
        PersistenceInterval = registrationOptions.PersistenceInterval,
        LoggerFactory = loggerFactory
      };

      return new SyncServer(options, transport);
    });

    // Register hosted service for graceful shutdown
    services.AddHostedService<SyncServerHostedService>();

    return services;
  }
}

/// <summary>
/// Configuration options for SyncServer DI registration.
/// </summary>
[PublicAPI]
public sealed class SyncServerRegistrationOptions
{
  /// <summary>
  /// Gets or sets the storage directory for documents.
  /// Defaults to <c>{AppContext.BaseDirectory}/documents</c>.
  /// </summary>
  public string StorageDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "documents");

  /// <summary>
  /// Gets or sets the interval between persistence flushes for dirty documents.
  /// Defaults to 5 seconds.
  /// </summary>
  public TimeSpan PersistenceInterval { get; set; } = TimeSpan.FromSeconds(5);
}
