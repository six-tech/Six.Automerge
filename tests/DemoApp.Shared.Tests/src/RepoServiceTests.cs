// Copyright (c) Six. All rights reserved.

using DemoApp.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DemoApp.Shared.Tests;

[Trait("Category", "Unit")]
public class RepoServiceTests
{
  static RepoServiceOptions CreateOptions(string? storageBaseDir = null) =>
    new()
    {
      HubPath = "/hubs/text-editor",
      AppName = "TestApp",
      StorageBaseDir = storageBaseDir
    };

  [Fact]
  public void Constructor_WithValidOptions_CreatesInstance()
  {
    var options = CreateOptions();

    var service = new RepoService(options);

    service.Should().NotBeNull();
  }

  [Fact]
  public void Constructor_WithNullOptions_ThrowsArgumentNullException()
  {
    Action act = () => new RepoService(null!);

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void Constructor_WithLoggerFactory_CreatesInstance()
  {
    var options = CreateOptions();

    var service = new RepoService(options, NullLoggerFactory.Instance);

    service.Should().NotBeNull();
  }

  [Fact]
  public void PeerId_BeforeInitialize_ReturnsNull()
  {
    var service = new RepoService(CreateOptions());

    service.PeerId.Should().BeNull();
  }

  [Fact]
  public void Repo_BeforeInitialize_ReturnsNull()
  {
    var service = new RepoService(CreateOptions());

    service.Repo.Should().BeNull();
  }

  [Fact]
  public void ConnectionState_BeforeInitialize_IsDisconnected()
  {
    var service = new RepoService(CreateOptions());

    service.ConnectionState.Should().Be(ConnectionState.Disconnected);
  }

  [Fact]
  public void PeerCount_BeforeInitialize_IsZero()
  {
    var service = new RepoService(CreateOptions());

    service.PeerCount.Should().Be(0);
  }

  [Fact]
  public void IsInitialized_BeforeInitialize_IsFalse()
  {
    var service = new RepoService(CreateOptions());

    service.IsInitialized.Should().BeFalse();
  }

  [Fact]
  public void Dispose_WhenNotInitialized_DoesNotThrow()
  {
    var service = new RepoService(CreateOptions());

    Action act = () => service.Dispose();

    act.Should().NotThrow();
  }

  [Fact]
  public void Dispose_CalledTwice_DoesNotThrow()
  {
    var service = new RepoService(CreateOptions());

    Action act = () =>
    {
      service.Dispose();
      service.Dispose();
    };

    act.Should().NotThrow();
  }

  [Fact]
  public void GetStorageDirectory_WithCustomBaseDir_ReturnsCustomPath()
  {
    string? original = Environment.GetEnvironmentVariable("REPLICA_INDEX");
    try
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", null);
      string customDir = Path.Combine(Path.GetTempPath(), "test-storage");
      var options = CreateOptions(storageBaseDir: customDir);
      var service = new RepoService(options);

      service.StorageDirectory.Should().Be(customDir);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", original);
    }
  }

  [Fact]
  public void GetStorageDirectory_WithDefaultBaseDir_ReturnsHomeBasedPath()
  {
    string? original = Environment.GetEnvironmentVariable("REPLICA_INDEX");
    try
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", null);
      var options = CreateOptions();
      var service = new RepoService(options);

      string expected = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayautomerge",
        "TestApp");

      service.StorageDirectory.Should().Be(expected);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", original);
    }
  }

  [Fact]
  public void GetStorageDirectory_WithReplicaIndex_AppendsReplicaSubfolder()
  {
    string? original = Environment.GetEnvironmentVariable("REPLICA_INDEX");
    try
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", "2");
      var options = CreateOptions();
      var service = new RepoService(options);

      string expected = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayautomerge",
        "TestApp",
        "replica-2");

      service.StorageDirectory.Should().Be(expected);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", original);
    }
  }

  [Fact]
  public void GetStorageDirectory_WithReplicaIndexAndCustomBaseDir_AppendsReplicaToCustomPath()
  {
    string? original = Environment.GetEnvironmentVariable("REPLICA_INDEX");
    try
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", "0");
      string customDir = Path.Combine(Path.GetTempPath(), "test-storage");
      var options = CreateOptions(storageBaseDir: customDir);
      var service = new RepoService(options);

      string expected = Path.Combine(customDir, "replica-0");
      service.StorageDirectory.Should().Be(expected);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", original);
    }
  }

  [Fact]
  public void GetStorageDirectory_WithEmptyReplicaIndex_ReturnsDefaultPath()
  {
    string? original = Environment.GetEnvironmentVariable("REPLICA_INDEX");
    try
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", "");
      var options = CreateOptions();
      var service = new RepoService(options);

      string expected = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".rayautomerge",
        "TestApp");

      service.StorageDirectory.Should().Be(expected);
    }
    finally
    {
      Environment.SetEnvironmentVariable("REPLICA_INDEX", original);
    }
  }

  [Fact]
  public void ConnectionStateChanged_Event_CanBeSubscribed()
  {
    var service = new RepoService(CreateOptions());
    bool eventFired = false;

    service.ConnectionStateChanged += (_, _) => eventFired = true;

    // Event should not fire just from subscribing
    eventFired.Should().BeFalse();
  }

  [Fact]
  public void PeerCountChanged_Event_CanBeSubscribed()
  {
    var service = new RepoService(CreateOptions());
    bool eventFired = false;

    service.PeerCountChanged += (_, _) => eventFired = true;

    // Event should not fire just from subscribing
    eventFired.Should().BeFalse();
  }

  [Fact]
  public async Task InitializeAsync_WithUnreachableHub_SetsConnectionStateToDisconnected()
  {
    // Use a non-routable address to ensure quick failure
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      HubUrl = "https://192.0.2.1:9999/hubs/text-editor",
      AppName = "TestApp",
      StorageBaseDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}")
    };

    var service = new RepoService(options, NullLoggerFactory.Instance);

    // InitializeAsync should not throw — it handles connection failure gracefully
    await service.InitializeAsync();

    // The repo should still be created (with storage), but connection failed
    service.Repo.Should().NotBeNull();
    service.PeerId.Should().NotBeNull();
    service.IsInitialized.Should().BeTrue();
    service.ConnectionState.Should().Be(ConnectionState.Disconnected);

    service.Dispose();

    // Cleanup storage dir
    string storageDir = options.StorageBaseDir;
    if(Directory.Exists(storageDir))
    {
      Directory.Delete(storageDir, true);
    }
  }

  [Fact]
  public async Task InitializeAsync_WithCancellationToken_WhenCancelled_ThrowsOperationCanceledException()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      HubUrl = "https://192.0.2.1:9999/hubs/text-editor",
      AppName = "TestApp",
      StorageBaseDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}")
    };

    var service = new RepoService(options, NullLoggerFactory.Instance);

    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    Func<Task> act = () => service.InitializeAsync(cts.Token);

    await act.Should().ThrowAsync<OperationCanceledException>();

    service.Dispose();

    if(Directory.Exists(options.StorageBaseDir))
    {
      Directory.Delete(options.StorageBaseDir, true);
    }
  }

  [Fact]
  public async Task InitializeAsync_WithCustomTimeout_UsesConfiguredTimeout()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      HubUrl = "https://192.0.2.1:9999/hubs/text-editor",
      AppName = "TestApp",
      StorageBaseDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}"),
      ConnectionTimeout = TimeSpan.FromMilliseconds(100)
    };

    var service = new RepoService(options, NullLoggerFactory.Instance);

    // Should complete quickly because the custom short timeout kicks in
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await service.InitializeAsync();
    sw.Stop();

    // With a 100ms timeout, the total connect attempt should be well under 5s
    sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

    // Repo should still be initialized despite connection failure
    service.IsInitialized.Should().BeTrue();
    service.ConnectionState.Should().Be(ConnectionState.Disconnected);

    service.Dispose();

    if(Directory.Exists(options.StorageBaseDir))
    {
      Directory.Delete(options.StorageBaseDir, true);
    }
  }

  [Fact]
  public void ConnectionTimeout_DefaultsTo10Seconds()
  {
    var options = CreateOptions();

    options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(10));
  }

  [Fact]
  public void HubPath_IsRequired()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/drawing",
      AppName = "DrawingApp"
    };

    options.HubPath.Should().Be("/hubs/drawing");
  }

  [Fact]
  public void ResolvedHubUrl_WithExplicitHubUrl_UsesHubUrl()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      HubUrl = "https://custom-server:5001/hubs/text-editor",
      AppName = "TestApp"
    };
    var service = new RepoService(options);

    service.ResolvedHubUrl.Should().Be("https://custom-server:5001/hubs/text-editor");
  }

  [Fact]
  public void ResolvedHubUrl_WithoutHubUrl_UsesDefaultBaseUrlPlusHubPath()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/editor",
      AppName = "TestApp"
    };
    var service = new RepoService(options);

    // Without env vars set, falls back to https://localhost:5001 + /hubs/editor
    service.ResolvedHubUrl.Should().Be("https://localhost:5001/hubs/editor");
  }

  [Fact]
  public async Task ListServerDocumentsAsync_BeforeInitialize_ThrowsInvalidOperationException()
  {
    var service = new RepoService(CreateOptions());

    Func<Task> act = () => service.ListServerDocumentsAsync();

    await act.Should().ThrowAsync<InvalidOperationException>();

    service.Dispose();
  }

  [Fact]
  public void HubConnection_BeforeInitialize_ReturnsNull()
  {
    var service = new RepoService(CreateOptions());

    service.HubConnection.Should().BeNull();
  }

  [Fact]
  public void DocumentPeerCount_BeforeInitialize_IsZero()
  {
    var service = new RepoService(CreateOptions());

    service.DocumentPeerCount.Should().Be(0);
  }

  [Fact]
  public void DocumentPeerCountChanged_Event_CanBeSubscribed()
  {
    var service = new RepoService(CreateOptions());
    bool eventFired = false;

    service.DocumentPeerCountChanged += (_, _) => eventFired = true;

    // Event should not fire just from subscribing
    eventFired.Should().BeFalse();
  }

  [Fact]
  public async Task GetDocumentPeersAsync_BeforeInitialize_ThrowsInvalidOperationException()
  {
    var service = new RepoService(CreateOptions());

    Func<Task> act = () => service.GetDocumentPeersAsync("test-doc-id");

    await act.Should().ThrowAsync<InvalidOperationException>();

    service.Dispose();
  }

  [Fact]
  public void DocumentsChanged_Event_CanBeSubscribed()
  {
    var service = new RepoService(CreateOptions());
    bool eventFired = false;

    service.DocumentsChanged += (_, _) => eventFired = true;

    // Event should not fire just from subscribing
    eventFired.Should().BeFalse();
  }

  [Fact]
  public async Task GetDocumentPeersAsync_WhenDisconnected_ThrowsInvalidOperationException()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      HubUrl = "https://192.0.2.1:9999/hubs/text-editor",
      AppName = "TestApp",
      StorageBaseDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}"),
      ConnectionTimeout = TimeSpan.FromMilliseconds(100)
    };

    var service = new RepoService(options, NullLoggerFactory.Instance);
    await service.InitializeAsync();

    // Service is initialized but disconnected (unreachable hub)
    service.ConnectionState.Should().Be(ConnectionState.Disconnected);

    Func<Task> act = () => service.GetDocumentPeersAsync("test-doc-id");

    await act.Should().ThrowAsync<InvalidOperationException>();

    service.Dispose();

    if(Directory.Exists(options.StorageBaseDir))
    {
      Directory.Delete(options.StorageBaseDir, true);
    }
  }
}
