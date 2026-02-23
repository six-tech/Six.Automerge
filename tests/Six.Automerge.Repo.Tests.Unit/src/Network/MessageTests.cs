using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Xunit;

namespace Six.Automerge.Repo.Test.Unit.Network;

[Trait("Category", "Network")]
public class MessageTests
{
    [Fact]
    public void SyncMessage_ShouldHaveCorrectMessageType()
    {
        var documentId = DocumentId.Generate();
        var message = new SyncMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        Assert.Equal("sync", message.MessageType);
    }

    [Fact]
    public void SyncMessage_ShouldStoreProperties()
    {
        var documentId = DocumentId.Generate();
        var data = new byte[] { 1, 2, 3, 4 };
        var senderId = PeerId.Generate();

        var message = new SyncMessage
        {
            DocumentId = documentId,
            Data = data,
            SenderId = senderId
        };

        Assert.Equal(documentId, message.DocumentId);
        Assert.Equal(data, message.Data);
        Assert.Equal(senderId, message.SenderId);
    }

    [Fact]
    public void RequestMessage_ShouldHaveCorrectMessageType()
    {
        var documentId = DocumentId.Generate();
        var message = new RequestMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        Assert.Equal("request", message.MessageType);
    }

    [Fact]
    public void RequestMessage_ShouldStoreProperties()
    {
        var documentId = DocumentId.Generate();
        var data = new byte[] { 1, 2, 3, 4 };
        var senderId = PeerId.Generate();

        var message = new RequestMessage
        {
            DocumentId = documentId,
            Data = data,
            SenderId = senderId
        };

        Assert.Equal(documentId, message.DocumentId);
        Assert.Equal(data, message.Data);
        Assert.Equal(senderId, message.SenderId);
    }

    [Fact]
    public void EphemeralMessage_ShouldHaveCorrectMessageType()
    {
        var documentId = DocumentId.Generate();
        var message = new EphemeralMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = SessionId.Generate()
        };

        Assert.Equal("ephemeral", message.MessageType);
    }

    [Fact]
    public void EphemeralMessage_ShouldStoreProperties()
    {
        var documentId = DocumentId.Generate();
        var data = new byte[] { 1, 2, 3, 4 };
        var senderId = PeerId.Generate();
        var sessionId = SessionId.Generate();
        var count = 42;

        var message = new EphemeralMessage
        {
            DocumentId = documentId,
            Data = data,
            SenderId = senderId,
            Count = count,
            SessionId = sessionId
        };

        Assert.Equal(documentId, message.DocumentId);
        Assert.Equal(data, message.Data);
        Assert.Equal(senderId, message.SenderId);
        Assert.Equal(count, message.Count);
        Assert.Equal(sessionId, message.SessionId);
    }

    [Fact]
    public void DocumentUnavailableMessage_ShouldHaveCorrectMessageType()
    {
        var documentId = DocumentId.Generate();
        var message = new DocumentUnavailableMessage
        {
            DocumentId = documentId
        };

        Assert.Equal("doc-unavailable", message.MessageType);
    }

    [Fact]
    public void DocumentUnavailableMessage_ShouldStoreProperties()
    {
        var documentId = DocumentId.Generate();
        var senderId = PeerId.Generate();

        var message = new DocumentUnavailableMessage
        {
            DocumentId = documentId,
            SenderId = senderId
        };

        Assert.Equal(documentId, message.DocumentId);
        Assert.Equal(senderId, message.SenderId);
    }

    [Fact]
    public void DocumentSnapshotMessage_ShouldStoreProperties()
    {
        var documentId = DocumentId.Generate();
        var senderId = PeerId.Generate();
        var data = new byte[] { 1, 2, 3 };

        var message = new DocumentSnapshotMessage
        {
            DocumentId = documentId,
            Data = data,
            SenderId = senderId
        };

        Assert.Equal("doc-snapshot", message.MessageType);
        Assert.Equal(documentId, message.DocumentId);
        Assert.Equal(data, message.Data);
        Assert.Equal(senderId, message.SenderId);
    }

    [Fact]
    public void RemoteHeadsChangedMessage_ShouldHaveCorrectMessageType()
    {
        var documentId = DocumentId.Generate();
        var message = new RemoteHeadsChangedMessage
        {
            DocumentId = documentId,
            NewHeads = new List<string> { "head1", "head2" }
        };

        Assert.Equal("remote-heads-changed", message.MessageType);
    }

    [Fact]
    public void RemoteHeadsChangedMessage_ShouldStoreProperties()
    {
        var documentId = DocumentId.Generate();
        var newHeads = new List<string> { "head1", "head2", "head3" };
        var senderId = PeerId.Generate();

        var message = new RemoteHeadsChangedMessage
        {
            DocumentId = documentId,
            NewHeads = newHeads,
            SenderId = senderId
        };

        Assert.Equal(documentId, message.DocumentId);
        Assert.Equal(newHeads, message.NewHeads);
        Assert.Equal(senderId, message.SenderId);
    }

    [Fact]
    public void RemoteSubscriptionChangeMessage_ShouldHaveCorrectMessageType()
    {
        var message = new RemoteSubscriptionChangeMessage
        {
            Add = new List<DocumentId> { DocumentId.Generate() }
        };

        Assert.Equal("remote-subscription-change", message.MessageType);
    }

    [Fact]
    public void RemoteSubscriptionChangeMessage_ShouldStoreAddList()
    {
        var add = new List<DocumentId> { DocumentId.Generate(), DocumentId.Generate() };
        var message = new RemoteSubscriptionChangeMessage
        {
            Add = add
        };

        Assert.Equal(add, message.Add);
        Assert.Null(message.Remove);
    }

    [Fact]
    public void RemoteSubscriptionChangeMessage_ShouldStoreRemoveList()
    {
        var remove = new List<DocumentId> { DocumentId.Generate(), DocumentId.Generate() };
        var message = new RemoteSubscriptionChangeMessage
        {
            Remove = remove
        };

        Assert.Equal(remove, message.Remove);
        Assert.Null(message.Add);
    }

    [Fact]
    public void WithSender_ShouldSetSenderIdOnSyncMessage()
    {
        var senderId = PeerId.Generate();
        var documentId = DocumentId.Generate();
        var message = new SyncMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        var updatedMessage = message.WithSender(senderId);

        Assert.IsType<SyncMessage>(updatedMessage);
        Assert.Equal(senderId, updatedMessage.SenderId);
        Assert.Equal(documentId, updatedMessage.DocumentId);
    }

    [Fact]
    public void WithSender_ShouldSetSenderIdOnRequestMessage()
    {
        var senderId = PeerId.Generate();
        var documentId = DocumentId.Generate();
        var message = new RequestMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        var updatedMessage = message.WithSender(senderId);

        Assert.IsType<RequestMessage>(updatedMessage);
        Assert.Equal(senderId, updatedMessage.SenderId);
    }

    [Fact]
    public void WithSender_ShouldSetSenderIdOnEphemeralMessage()
    {
        var senderId = PeerId.Generate();
        var sessionId = SessionId.Generate();
        var documentId = DocumentId.Generate();
        var message = new EphemeralMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = sessionId
        };

        var updatedMessage = message.WithSender(senderId);

        Assert.IsType<EphemeralMessage>(updatedMessage);
        var ephemeralMessage = (EphemeralMessage)updatedMessage;
        Assert.Equal(senderId, ephemeralMessage.SenderId);
        Assert.Equal(1, ephemeralMessage.Count);
        Assert.Equal(sessionId, ephemeralMessage.SessionId);
    }

    [Fact]
    public void WithSender_ShouldSetSenderIdOnDocumentUnavailableMessage()
    {
        var senderId = PeerId.Generate();
        var documentId = DocumentId.Generate();
        var message = new DocumentUnavailableMessage
        {
            DocumentId = documentId
        };

        var updatedMessage = message.WithSender(senderId);

        Assert.IsType<DocumentUnavailableMessage>(updatedMessage);
        Assert.Equal(senderId, updatedMessage.SenderId);
    }

    [Fact]
    public void WithSender_ShouldSetSenderIdOnRemoteHeadsChangedMessage()
    {
        var senderId = PeerId.Generate();
        var documentId = DocumentId.Generate();
        var message = new RemoteHeadsChangedMessage
        {
            DocumentId = documentId,
            NewHeads = new List<string> { "head1" }
        };

        var updatedMessage = message.WithSender(senderId);

        Assert.IsType<RemoteHeadsChangedMessage>(updatedMessage);
        Assert.Equal(senderId, updatedMessage.SenderId);
    }

    [Fact]
    public void WithSender_ShouldSetSenderIdOnRemoteSubscriptionChangeMessage()
    {
        var senderId = PeerId.Generate();
        var message = new RemoteSubscriptionChangeMessage
        {
            Add = new List<DocumentId> { DocumentId.Generate() }
        };

        var updatedMessage = message.WithSender(senderId);

        Assert.IsType<RemoteSubscriptionChangeMessage>(updatedMessage);
        Assert.Equal(senderId, updatedMessage.SenderId);
    }

    [Fact]
    public void IsSyncMessage_ShouldReturnTrueForSyncMessage()
    {
        var documentId = DocumentId.Generate();
        var message = new SyncMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        Assert.True(message.IsSyncMessage());
    }

    [Fact]
    public void IsSyncMessage_ShouldReturnTrueForRequestMessage()
    {
        var documentId = DocumentId.Generate();
        var message = new RequestMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        Assert.True(message.IsSyncMessage());
    }

    [Fact]
    public void IsSyncMessage_ShouldReturnFalseForEphemeralMessage()
    {
        var documentId = DocumentId.Generate();
        var message = new EphemeralMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = SessionId.Generate()
        };

        Assert.False(message.IsSyncMessage());
    }

    [Fact]
    public void IsEphemeralMessage_ShouldReturnTrueForEphemeralMessage()
    {
        var documentId = DocumentId.Generate();
        var message = new EphemeralMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 },
            Count = 1,
            SessionId = SessionId.Generate()
        };

        Assert.True(message.IsEphemeralMessage());
    }

    [Fact]
    public void IsEphemeralMessage_ShouldReturnFalseForSyncMessage()
    {
        var documentId = DocumentId.Generate();
        var message = new SyncMessage
        {
            DocumentId = documentId,
            Data = new byte[] { 1, 2, 3 }
        };

        Assert.False(message.IsEphemeralMessage());
    }
}
