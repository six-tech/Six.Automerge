using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Projects;

namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;


/// <summary>
/// Shared test fixture for SignalR integration tests.
/// This fixture is created once for all tests in the collection and disposed after all the tests complete.
/// It manages the lifecycle of the SignalR hub server via Aspire and provides the hub URL.
/// </summary>
[PublicAPI]
public class AspireTestFixture : IAsyncLifetime
{
  static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(15);
  const string SignalRHubResourceName = "signalr-hub";
  DistributedApplication _app = null!;
  string _hubUrl = null!;


  /// <summary>
  /// Initializes the test fixture by starting resources managed by Aspire.
  /// This method is called once before any tests in the collection run.
  /// </summary>
  /// <remarks>
  /// The port is fixed with: <c>"DcpPublisher:RandomizePorts=false"</c> so tests are deterministic.
  /// </remarks>
  public async Task InitializeAsync()
  {
    // Arrange
    var cancellationToken = new CancellationTokenSource(_defaultTimeout).Token;
    var appHost =
      await DistributedApplicationTestingBuilder.CreateAsync<Six_Tests_AppHost>(
        args: ["DcpPublisher:RandomizePorts=false"], // We disable port randomization for tests
        cancellationToken: cancellationToken);

    appHost.Services.AddLogging(logging =>
    {
      logging.SetMinimumLevel(LogLevel.Debug);
      logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
      logging.AddFilter("Aspire.", LogLevel.Debug);
    });

    appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
    {
      clientBuilder.AddStandardResilienceHandler();
    });

    // Build and start the application
    _app = await appHost.BuildAsync(cancellationToken).WaitAsync(_defaultTimeout, cancellationToken);
    await _app.StartAsync(cancellationToken).WaitAsync(_defaultTimeout, cancellationToken);

    // Wait for SignalR hub to be healthy
    await _app.ResourceNotifications
      .WaitForResourceHealthyAsync(SignalRHubResourceName, cancellationToken)
      .WaitAsync(_defaultTimeout, cancellationToken);

    // Get the hub URL from the resource using the model
    var resourceModel = _app.Services.GetRequiredService<DistributedApplicationModel>();
    var signalRResource = resourceModel.Resources.Single(r => r.Name == SignalRHubResourceName);

    var httpEndpoint = signalRResource.Annotations.OfType<EndpointAnnotation>()
      .First(e => e.Name == "http");

    var allocatedEndpoint = httpEndpoint.AllocatedEndpoint
      ?? throw new InvalidOperationException("HTTP endpoint was not allocated for signalr-hub");

    _hubUrl = $"{allocatedEndpoint.UriString}/hubs/documents";
  }

  /// <summary>
  /// Gets the URL to connect to the SignalR hub.
  /// </summary>
  /// <returns>The hub connection URL.</returns>
  public string GetHubUrl() => _hubUrl;

  /// <summary>
  /// Cleans up the test fixture by disposing the Aspire application and SignalR hub server.
  /// This method is called once after all tests in the collection complete.
  /// </summary>
  public async Task DisposeAsync()
  {
    await _app.DisposeAsync();
  }
}