using Microsoft.AspNetCore.SignalR.Client;

namespace Six.Automerge.Repo.Network.SignalR.Client;

public interface ISignalRConnectionService : IDisposable, IAsyncDisposable
{
  Task<HubConnection> GetConnection();
  Task Start();
  Task Stop();
}