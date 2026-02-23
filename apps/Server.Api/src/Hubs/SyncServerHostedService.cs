// Copyright (c) Six. All rights reserved.

using Microsoft.AspNetCore.SignalR;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.SyncServer;

namespace Server.Api.Hubs;

/// <summary>
/// Hosted service that ensures <see cref="SyncServer"/> is shut down gracefully
/// when the application stops. Also subscribes to <see cref="SyncServer.DocumentAvailable"/>
/// and broadcasts new document availability to connected SignalR clients.
/// </summary>
sealed class SyncServerHostedService : IHostedService
{
  readonly SyncServer _syncServer;
  readonly IHubContext<DocumentsHub, IDocumentsHubClient> _hubContext;

  public SyncServerHostedService(
    SyncServer syncServer,
    IHubContext<DocumentsHub, IDocumentsHubClient> hubContext)
  {
    _syncServer = syncServer;
    _hubContext = hubContext;
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _syncServer.DocumentAvailable += OnDocumentAvailable;
    return Task.CompletedTask;
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _syncServer.DocumentAvailable -= OnDocumentAvailable;
    await _syncServer.DisposeAsync().ConfigureAwait(false);
  }

  void OnDocumentAvailable(object? sender, DocumentAvailableEventArgs e)
  {
    _ = _hubContext.Clients.All.OnDocumentAvailable(e.DocumentId);
  }
}
