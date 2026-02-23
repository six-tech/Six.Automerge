using MessagePack;
using MessagePack.Resolvers;
using Server.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (includes service discovery, health checks, etc.)
builder.AddServiceDefaults();

// Add SignalR with MessagePack (LZ4 compression) + JSON protocols
builder.Services
  .AddSignalR()
  .AddMessagePackProtocol(options =>
  {
    options.SerializerOptions = MessagePackSerializerOptions.Standard
      .WithCompression(MessagePackCompression.Lz4Block)
      .WithResolver(ContractlessStandardResolver.Instance);
  })
  .AddJsonProtocol();

// Register SyncServer infrastructure (transport, sync server, hosted service)
builder.Services.AddSyncServer(options =>
{
  var envDir = Environment.GetEnvironmentVariable("SERVER_STORAGE_DIR");
  if(!string.IsNullOrWhiteSpace(envDir))
    options.StorageDirectory = envDir;
});

builder.Services
  .AddCors(options =>
  {
    options.AddPolicy("AllowAll", policy =>
    {
      policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
  });

if(builder.Environment.IsDevelopment())
{
  builder.Services.AddOpenApi();
}

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.MapDefaultEndpoints();

app.MapHub<DocumentsHub>("/hubs/documents");
app.MapHub<DocumentsHub>("/hubs/editor");
app.MapHub<DocumentsHub>("/hubs/drawing");
app.MapHub<DocumentsHub>("/hubs/task-board");

if(app.Environment.IsDevelopment())
{
  app.MapOpenApi();
}

await app.RunAsync();
