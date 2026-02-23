namespace Six.Automerge.Repository.Network;

/// <summary>
/// Helper methods for working with repo messages.
/// </summary>
public static class MessageExtensions
{
  /// <summary>
  /// Creates a copy of the message with the sender ID set.
  /// </summary>
  public static IRepoMessage WithSender(this IRepoMessage message, PeerId senderId)
  {
    return message switch
    {
      SyncMessage sync => sync with { SenderId = senderId },
      RequestMessage req => req with { SenderId = senderId },
      EphemeralMessage eph => eph with { SenderId = senderId },
      DocumentUnavailableMessage unavail => unavail with { SenderId = senderId },
      DocumentSnapshotMessage snapshot => snapshot with { SenderId = senderId },
      RemoteHeadsChangedMessage heads => heads with { SenderId = senderId },
      RemoteSubscriptionChangeMessage sub => sub with { SenderId = senderId },
      _ => throw new ArgumentException($"Unknown message type: {message.GetType().Name}")
    };
  }

  /// <summary>
  /// Creates a copy of the message with the target peer ID set.
  /// </summary>
  public static IRepoMessage WithTarget(this IRepoMessage message, PeerId targetPeerId)
  {
    return message switch
    {
      SyncMessage sync => sync with { TargetPeerId = targetPeerId },
      RequestMessage req => req with { TargetPeerId = targetPeerId },
      EphemeralMessage eph => eph with { TargetPeerId = targetPeerId },
      DocumentUnavailableMessage unavail => unavail with { TargetPeerId = targetPeerId },
      DocumentSnapshotMessage snapshot => snapshot with { TargetPeerId = targetPeerId },
      RemoteHeadsChangedMessage heads => heads with { TargetPeerId = targetPeerId },
      RemoteSubscriptionChangeMessage sub => sub with { TargetPeerId = targetPeerId },
      _ => throw new ArgumentException($"Unknown message type: {message.GetType().Name}")
    };
  }

  /// <summary>
  /// Type guard: checks if message is a sync-related message.
  /// </summary>
  public static bool IsSyncMessage(this IRepoMessage message)
    => message is SyncMessage or RequestMessage;

  /// <summary>
  /// Type guard: checks if message is ephemeral.
  /// </summary>
  public static bool IsEphemeralMessage(this IRepoMessage message)
    => message is EphemeralMessage;
}
