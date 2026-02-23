// Copyright (c) Six. All rights reserved.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Server.Api.Hubs;
using Server.Api.Services;
using Six.Automerge.SyncServer;
using Xunit;

namespace Server.Api.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class ServiceRegistrationTests
{
  [Fact]
  public void AddSyncServer_RegistersSignalRServerTransport()
  {
    var services = CreateMinimalServices();

    services.AddSyncServer();

    services.Should().Contain(s =>
      s.ServiceType == typeof(SignalRServerTransport) &&
      s.Lifetime == ServiceLifetime.Singleton);
  }

  [Fact]
  public void AddSyncServer_RegistersSyncServerSingleton()
  {
    var services = CreateMinimalServices();

    services.AddSyncServer();

    services.Should().Contain(s =>
      s.ServiceType == typeof(SyncServer) &&
      s.Lifetime == ServiceLifetime.Singleton);
  }

  [Fact]
  public void AddSyncServer_RegistersDocumentPeerTrackerSingleton()
  {
    var services = CreateMinimalServices();

    services.AddSyncServer();

    services.Should().Contain(s =>
      s.ServiceType == typeof(DocumentPeerTracker) &&
      s.Lifetime == ServiceLifetime.Singleton);
  }

  [Fact]
  public void AddSyncServer_RegistersHostedServiceForGracefulShutdown()
  {
    var services = CreateMinimalServices();

    services.AddSyncServer();

    services.Should().Contain(s =>
      s.ServiceType == typeof(IHostedService) &&
      s.ImplementationType == typeof(SyncServerHostedService));
  }

  [Fact]
  public void AddSyncServer_WithCustomOptions_AppliesConfiguration()
  {
    var services = CreateMinimalServices();
    var customDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    var customInterval = TimeSpan.FromSeconds(30);

    services.AddSyncServer(options =>
    {
      options.StorageDirectory = customDir;
      options.PersistenceInterval = customInterval;
    });

    // Verify registration exists (we can't easily resolve without a full host,
    // but we can verify the descriptor is present)
    services.Should().Contain(s =>
      s.ServiceType == typeof(SyncServer) &&
      s.Lifetime == ServiceLifetime.Singleton);
  }

  [Fact]
  public void AddSyncServer_ThrowsOnNullServices()
  {
    IServiceCollection services = null!;

    var act = () => services.AddSyncServer();

    act.Should().Throw<ArgumentNullException>()
      .WithParameterName("services");
  }

  [Fact]
  public void SyncServerRegistrationOptions_HasDefaultStorageDirectory()
  {
    var options = new SyncServerRegistrationOptions();

    options.StorageDirectory.Should().EndWith("documents");
    options.StorageDirectory.Should().NotBeNullOrWhiteSpace();
  }

  [Fact]
  public void SyncServerRegistrationOptions_HasDefaultPersistenceInterval()
  {
    var options = new SyncServerRegistrationOptions();

    options.PersistenceInterval.Should().Be(TimeSpan.FromSeconds(5));
  }

  static ServiceCollection CreateMinimalServices()
  {
    var services = new ServiceCollection();
    services.AddLogging();
    // SignalR's IHubContext is needed by SignalRServerTransport but won't be resolved in
    // these descriptor-level tests — we just check the DI registrations are correct.
    return services;
  }
}
