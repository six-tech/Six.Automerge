// Copyright (c) Six. All rights reserved.

using FluentAssertions;
using Six.Automerge.Repository.Network.SignalR;
using Server.Api.Services;
using Xunit;

namespace Server.Api.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class DocumentPeerTrackerTests
{
  readonly DocumentPeerTracker _tracker = new();

  static PeerInfoDto MakePeer(string peerId) => new()
  {
    PeerId = peerId,
    Metadata = null
  };

  #region AddPeerToDocument

  [Fact]
  public void AddPeerToDocument_TracksThePeer()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));

    var peers = _tracker.GetDocumentPeers("doc-1");
    peers.Should().ContainSingle(p => p.PeerId == "peer-A");
  }

  [Fact]
  public void AddPeerToDocument_MultiplePeers_TracksAll()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));

    var peers = _tracker.GetDocumentPeers("doc-1");
    peers.Should().HaveCount(2);
    peers.Select(p => p.PeerId).Should().BeEquivalentTo(["peer-A", "peer-B"]);
  }

  [Fact]
  public void AddPeerToDocument_SamePeerTwice_UpdatesEntry()
  {
    var peerWithMeta = new PeerInfoDto
    {
      PeerId = "peer-A",
      Metadata = new PeerMetadataDto { StorageId = "storage-1" }
    };

    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", peerWithMeta);

    var peers = _tracker.GetDocumentPeers("doc-1");
    peers.Should().ContainSingle(p => p.PeerId == "peer-A");
    peers[0].Metadata?.StorageId.Should().Be("storage-1");
  }

  [Fact]
  public void AddPeerToDocument_MultipleDocuments_TracksPerDocument()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-2", MakePeer("peer-A"));

    _tracker.GetDocumentPeers("doc-1").Should().ContainSingle(p => p.PeerId == "peer-A");
    _tracker.GetDocumentPeers("doc-2").Should().ContainSingle(p => p.PeerId == "peer-A");
  }

  #endregion

  #region RemovePeerFromDocument

  [Fact]
  public void RemovePeerFromDocument_RemovesThePeer()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));

    _tracker.RemovePeerFromDocument("doc-1", "peer-A");

    var peers = _tracker.GetDocumentPeers("doc-1");
    peers.Should().ContainSingle(p => p.PeerId == "peer-B");
  }

  [Fact]
  public void RemovePeerFromDocument_NonExistentPeer_DoesNotThrow()
  {
    var act = () => _tracker.RemovePeerFromDocument("doc-1", "unknown-peer");
    act.Should().NotThrow();
  }

  [Fact]
  public void RemovePeerFromDocument_NonExistentDocument_DoesNotThrow()
  {
    var act = () => _tracker.RemovePeerFromDocument("unknown-doc", "peer-A");
    act.Should().NotThrow();
  }

  [Fact]
  public void RemovePeerFromDocument_LastPeer_CleansUpDocument()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.RemovePeerFromDocument("doc-1", "peer-A");

    _tracker.GetDocumentPeers("doc-1").Should().BeEmpty();
    _tracker.GetDocumentPeerCount("doc-1").Should().Be(0);
  }

  #endregion

  #region RemovePeerFromAllDocuments

  [Fact]
  public void RemovePeerFromAllDocuments_RemovesFromAllDocuments()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-2", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-3", MakePeer("peer-A"));

    var affectedDocs = _tracker.RemovePeerFromAllDocuments("peer-A");

    affectedDocs.Should().BeEquivalentTo(["doc-1", "doc-2", "doc-3"]);
    _tracker.GetDocumentPeers("doc-1").Should().BeEmpty();
    _tracker.GetDocumentPeers("doc-2").Should().BeEmpty();
    _tracker.GetDocumentPeers("doc-3").Should().BeEmpty();
  }

  [Fact]
  public void RemovePeerFromAllDocuments_DoesNotAffectOtherPeers()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));

    _tracker.RemovePeerFromAllDocuments("peer-A");

    var peers = _tracker.GetDocumentPeers("doc-1");
    peers.Should().ContainSingle(p => p.PeerId == "peer-B");
  }

  [Fact]
  public void RemovePeerFromAllDocuments_UnknownPeer_ReturnsEmptyList()
  {
    var affected = _tracker.RemovePeerFromAllDocuments("unknown-peer");
    affected.Should().BeEmpty();
  }

  #endregion

  #region GetDocumentPeers (with exclusion)

  [Fact]
  public void GetDocumentPeers_WithExcludePeerId_ExcludesThatPeer()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-C"));

    var peers = _tracker.GetDocumentPeers("doc-1", excludePeerId: "peer-B");

    peers.Should().HaveCount(2);
    peers.Select(p => p.PeerId).Should().BeEquivalentTo(["peer-A", "peer-C"]);
  }

  [Fact]
  public void GetDocumentPeers_WithNullExclude_ReturnsAll()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));

    var peers = _tracker.GetDocumentPeers("doc-1", excludePeerId: null);
    peers.Should().HaveCount(2);
  }

  [Fact]
  public void GetDocumentPeers_UnknownDocument_ReturnsEmptyList()
  {
    var peers = _tracker.GetDocumentPeers("unknown-doc");
    peers.Should().BeEmpty();
  }

  #endregion

  #region GetDocumentPeerCount (with exclusion)

  [Fact]
  public void GetDocumentPeerCount_ReturnsCorrectCount()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));

    _tracker.GetDocumentPeerCount("doc-1").Should().Be(2);
  }

  [Fact]
  public void GetDocumentPeerCount_WithExcludePeerId_ExcludesThatPeer()
  {
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-A"));
    _tracker.AddPeerToDocument("doc-1", MakePeer("peer-B"));

    _tracker.GetDocumentPeerCount("doc-1", excludePeerId: "peer-A").Should().Be(1);
  }

  [Fact]
  public void GetDocumentPeerCount_UnknownDocument_ReturnsZero()
  {
    _tracker.GetDocumentPeerCount("unknown-doc").Should().Be(0);
  }

  #endregion

  #region Concurrent Operations

  [Fact]
  public async Task ConcurrentAddPeers_AllTracked()
  {
    var tasks = Enumerable.Range(0, 100).Select(i =>
      Task.Run(() => _tracker.AddPeerToDocument("doc-1", MakePeer($"peer-{i}"))));

    await Task.WhenAll(tasks);

    _tracker.GetDocumentPeerCount("doc-1").Should().Be(100);
  }

  [Fact]
  public async Task ConcurrentAddAndRemove_DoesNotThrow()
  {
    // Pre-populate
    for(int i = 0; i < 50; i++)
      _tracker.AddPeerToDocument("doc-1", MakePeer($"peer-{i}"));

    var addTasks = Enumerable.Range(50, 50).Select(i =>
      Task.Run(() => _tracker.AddPeerToDocument("doc-1", MakePeer($"peer-{i}"))));

    var removeTasks = Enumerable.Range(0, 50).Select(i =>
      Task.Run(() => _tracker.RemovePeerFromDocument("doc-1", $"peer-{i}")));

    var act = () => Task.WhenAll(addTasks.Concat(removeTasks));
    await act.Should().NotThrowAsync();
  }

  [Fact]
  public async Task ConcurrentRemovePeerFromAllDocuments_DoesNotThrow()
  {
    for(int i = 0; i < 20; i++)
    {
      for(int j = 0; j < 5; j++)
        _tracker.AddPeerToDocument($"doc-{j}", MakePeer($"peer-{i}"));
    }

    var tasks = Enumerable.Range(0, 20).Select(i =>
      Task.Run(() => _tracker.RemovePeerFromAllDocuments($"peer-{i}")));

    var act = () => Task.WhenAll(tasks);
    await act.Should().NotThrowAsync();
  }

  #endregion
}
