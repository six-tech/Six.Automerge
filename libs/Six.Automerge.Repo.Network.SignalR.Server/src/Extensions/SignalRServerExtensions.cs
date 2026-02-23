using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.DependencyInjection;

namespace Six.Automerge.Repository.Network.SignalR.Server.Extensions;

/// <summary>
/// Extension methods for configuring SignalR server with DocumentsHub.
/// </summary>
[PublicAPI]
public static class SignalRServerExtensions
{
  /// <summary>
  /// Adds the DocumentsHub to the SignalR server with MessagePack protocol and LZ4 compression.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>An <see cref="ISignalRServerBuilder"/> for further configuration.</returns>
  /// <remarks>
  /// This method configures SignalR with:
  /// <list type="bullet">
  ///   <item><description>MessagePack protocol with LZ4 block compression for bandwidth efficiency</description></item>
  ///   <item><description>Standard MessagePack resolver for broad compatibility</description></item>
  ///   <item><description>JSON fallback protocol for browser compatibility</description></item>
  /// </list>
  ///
  /// <para>
  /// <strong>Usage Example:</strong>
  /// <code>
  /// // In Program.cs or Startup.cs
  /// builder.Services.AddSignalRDocumentsHub();
  ///
  /// // Map the hub endpoint
  /// app.MapHub&lt;DocumentsHub&gt;("/hubs/documents");
  /// </code>
  /// </para>
  ///
  /// <para>
  /// <strong>With Additional Configuration:</strong>
  /// <code>
  /// builder.Services.AddSignalRDocumentsHub()
  ///   .AddHubOptions&lt;DocumentsHub&gt;(options =>
  ///   {
  ///     options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
  ///     options.EnableDetailedErrors = true;
  ///   });
  /// </code>
  /// </para>
  /// </remarks>
  public static ISignalRServerBuilder AddSignalRDocumentsHub(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    var signalRBuilder = services.AddSignalR()
      .AddMessagePackProtocol(options =>
      {
        // Use LZ4 compression for MessagePack to reduce bandwidth usage
        // This is especially important for large sync messages (can reduce size by 60-80%)
        options.SerializerOptions = MessagePackSerializerOptions.Standard
          .WithCompression(MessagePackCompression.Lz4Block)
          .WithResolver(ContractlessStandardResolver.Instance);
      })
      .AddJsonProtocol(); // Add JSON as fallback protocol

    return signalRBuilder;
  }

  /// <summary>
  /// Adds the DocumentsHub to the SignalR server with JSON protocol only.
  /// </summary>
  /// <param name="services">The service collection.</param>
  /// <returns>An <see cref="ISignalRServerBuilder"/> for further configuration.</returns>
  /// <remarks>
  /// Use this method when you need JSON-only communication (e.g., for debugging or when MessagePack
  /// is not suitable for your clients). Note that JSON protocol uses more bandwidth than MessagePack.
  ///
  /// <para>
  /// <strong>Usage Example:</strong>
  /// <code>
  /// builder.Services.AddSignalRDocumentsHubWithJson();
  /// app.MapHub&lt;DocumentsHub&gt;("/hubs/documents");
  /// </code>
  /// </para>
  /// </remarks>
  public static ISignalRServerBuilder AddSignalRDocumentsHubWithJson(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    var signalRBuilder = services.AddSignalR()
      .AddJsonProtocol();

    return signalRBuilder;
  }
}