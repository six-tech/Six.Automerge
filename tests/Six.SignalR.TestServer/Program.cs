using Six.Automerge.Repository.Network.SignalR.Server;
using Six.Automerge.Repository.Network.SignalR.Server.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure SignalR with MessagePack protocol and LZ4 compression
builder.Services.AddSignalRDocumentsHub();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Trace);
});

// Add health checks for Aspire
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map the hub
app.MapHub<DocumentsHub>("/hubs/documents");

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();
