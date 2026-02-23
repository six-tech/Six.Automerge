using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Configure SignalR Hub Test Server
builder.AddProject<Projects.Six_SignalR_TestServer>("signalr-hub")
  .WithHttpEndpoint(port: 8080, name: "http", isProxied: false)
  .WithHttpHealthCheck("/health");

builder.Build().Run();
