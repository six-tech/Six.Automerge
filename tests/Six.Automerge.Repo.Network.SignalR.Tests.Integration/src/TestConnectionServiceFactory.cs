using Microsoft.Extensions.Options;

namespace Six.Automerge.Repo.Network.SignalR.Tests.Integration;

/// <summary>
/// Factory for creating test SignalR connection services.
/// </summary>
public static class TestConnectionServiceFactory
{
  /// <summary>
  /// Creates a SignalR connection service configured for the test server.
  /// </summary>
  /// <param name="hubUrl">The hub URL to connect to.</param>
  /// <param name="useMessagePack">Whether to use MessagePack protocol (default: true).</param>
  /// <returns>A configured connection service instance.</returns>
  public static ISignalRConnectionService Create(string hubUrl, bool useMessagePack = true)
  {
    var loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddConsole();
      builder.SetMinimumLevel(LogLevel.Debug);
    });

    var options = new SignalRConnectionServiceOptions
    {
      HubUrl = hubUrl,
      UseMessagePackProtocol = useMessagePack,
      EnableAutomaticReconnect = false, // Disable for predictable testing
      AutoStart = true
    };

    var optionsMonitor = new TestOptionsMonitor(options);

    return new SignalRConnectionService(loggerFactory, optionsMonitor, "test");
  }

  class TestOptionsMonitor : IOptionsMonitor<SignalRConnectionServiceOptions>
  {
    readonly SignalRConnectionServiceOptions _options;

    public TestOptionsMonitor(SignalRConnectionServiceOptions options)
    {
      _options = options;
    }

    public SignalRConnectionServiceOptions CurrentValue => _options;

    public SignalRConnectionServiceOptions Get(string? name) => _options;

    public IDisposable? OnChange(Action<SignalRConnectionServiceOptions, string?> listener) => null;
  }
}
