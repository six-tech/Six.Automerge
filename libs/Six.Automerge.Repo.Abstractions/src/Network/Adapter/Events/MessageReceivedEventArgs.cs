namespace Six.Automerge.Repository.Network;

/// <summary>
/// Event args for received message.
/// </summary>
[PublicAPI]
public sealed class MessageReceivedEventArgs : EventArgs
{
  public required IRepoMessage Message { get; init; }
}