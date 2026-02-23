using System.Text.Json.Serialization;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable ClassNeverInstantiated.Global
namespace Six.Automerge.Repo.Network.SignalR.Client;

/// <summary>
/// <para>
/// SignalRConnectionService is a service that provides simple way to connect to a SignalR hub from the client apps
/// and take care of the connection lifecycle.
/// </para>
/// <para>
/// It reuses existing connection if it's already created to avoid unecessary connections (which should be avoided).
/// The best practice is to create a single connection for a SignalR hub and reuse it across the application.
/// </para>
/// <para>
/// It also supports keyed services, so you can have multiple SignalR hubs (and connections to them)
/// in the same application. See more how it works in the SignalRConnectionServiceExtensions class.
/// </para>
/// <para>
/// For example, when you want to inject the (keyed) SignalR connection
/// service into another service that depends on it:
/// <c>
/// [FromKeyedServices("DocumentHub")] ISignalRConnectionService connectionService
/// </c>
/// </para>
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class SignalRConnectionService : ISignalRConnectionService
{
  readonly ILogger _logger;
  readonly ILoggerFactory _loggerFactory;
  readonly SignalRConnectionServiceOptions _options;
  HubConnection? _connection;

  public SignalRConnectionService(ILoggerFactory loggerFactory, IOptionsMonitor<SignalRConnectionServiceOptions> options, string name)
  {
    _options = options.Get(name);
    _loggerFactory = loggerFactory;
    _logger = loggerFactory.CreateLogger<SignalRConnectionService>();
  }

  public async Task<HubConnection> GetConnection()
  {
    if(_connection is null)
    {
      _logger.LogInformation("Creating SignalR connection to: {Url}", _options.HubUrl);

      var builder = new HubConnectionBuilder()
        .WithUrl(_options.HubUrl);

      if(_options.UseMessagePackProtocol)
      {
        builder.AddMessagePackProtocol(options =>
        {
          // Use LZ4 compression for MessagePack (this reduces the size of messages even further)
          options.SerializerOptions =
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
        });
      }
      else
      {
        builder.AddJsonProtocol(options =>
        {
          // Ignore null values in DTOs when writing JSON
          options.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
      }

      if(_options.EnableAutomaticReconnect)
        builder = builder.WithAutomaticReconnect();

      try
      {
        _connection = builder.Build();
        _logger.LogInformation("SignalR connection to: {Url} created successfully", _options.HubUrl);
      }
      catch(Exception ex)
      {
        _logger.LogError("Failed to create a SignalR connection: {Url}. Failed with error: {Error}", _options.HubUrl,
          ex.Message);
      }
    }

    await Start();

    return _connection ??
           throw new NullReferenceException($"SignalR connection to: {_options.HubUrl} is null reference");
  }

  public async Task Start()
  {
    if(_connection is not null && _options.AutoStart && _connection.State == HubConnectionState.Disconnected)
    {
      _logger.LogInformation("Starting SignalR connection to: {Url}", _options.HubUrl);
      await _connection.StartAsync();
    }
  }

  public async Task Stop()
  {
    if(_connection is not null && _connection.State is HubConnectionState.Connected)
    {
      _logger.LogInformation("Stopping SignalR connection to: {Url}", _options.HubUrl);
      await _connection.StopAsync();
    }
  }

  public void Dispose()
  {
    DisposeAsync().GetAwaiter().GetResult();
  }

  public async ValueTask DisposeAsync()
  {
    if(_connection is not null)
    {
      await _connection.DisposeAsync();
      _connection = null;
    }
  }
}