using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Network.SignalR;

/// <summary>
/// Data transfer object for SignalR messages carrying Automerge sync protocol messages.
/// </summary>
/// <remarks>
/// This DTO wraps <see cref="IRepoMessage"/> instances for transport over SignalR.
/// The <see cref="Data"/> field contains the serialized message payload specific to each message type.
/// Supports both JSON and MessagePack serialization with LZ4 compression.
/// </remarks>
[PublicAPI]
[MessagePackObject]
public readonly record struct MessageDto
{
  /// <summary>
  /// The type discriminator for the message (e.g., "sync", "request", "ephemeral").
  /// </summary>
  /// <remarks>
  /// Must match the <see cref="IRepoMessage.MessageType"/> of the underlying message.
  /// Used for deserialization dispatch on the receiving end.
  /// </remarks>
  [Key(0)]
  public required string MessageType { get; init; }

  /// <summary>
  /// The document ID this message relates to (if applicable).
  /// </summary>
  /// <remarks>
  /// Used for SignalR group routing. Messages are broadcasted to all peers subscribed to this document.
  /// Null for non-document-specific messages (though rare in Automerge sync protocol).
  /// </remarks>
  [Key(1)]
  public string? DocumentId { get; init; }

  /// <summary>
  /// The peer ID of the message sender.
  /// </summary>
  /// <remarks>
  /// Set by the client before sending. Used for message attribution and preventing echo-back.
  /// </remarks>
  [Key(2)]
  public required string SenderId { get; init; }

  /// <summary>
  /// The serialized message payload.
  /// </summary>
  /// <remarks>
  /// Contains the type-specific data for the message (e.g., sync state, document heads, ephemeral data).
  /// Format depends on the <see cref="MessageType"/>. Typically serialized using MessagePack or JSON.
  /// </remarks>
  [Key(3)]
  public required byte[] Data { get; init; }

  /// <summary>
  /// Creates a MessageDto from an <see cref="IRepoMessage"/>.
  /// </summary>
  /// <param name="message">The repo message to convert.</param>
  /// <param name="serializedData">The serialized payload for the message.</param>
  /// <returns>A new MessageDto instance.</returns>
  /// <exception cref="ArgumentNullException">Thrown if message or senderId is null.</exception>
  public static MessageDto FromRepoMessage(IRepoMessage message, byte[] serializedData)
  {
    ArgumentNullException.ThrowIfNull(message);
    ArgumentNullException.ThrowIfNull(message.SenderId);

    return new MessageDto
    {
      MessageType = message.MessageType,
      DocumentId = message.DocumentId?.Value,
      SenderId = message.SenderId.Value,
      Data = serializedData
    };
  }
}
