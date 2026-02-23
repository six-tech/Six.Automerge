// Copyright (c) Six. All rights reserved.

using System.Collections.Concurrent;
using Six.Automerge.Repository.Network.SignalR;

namespace Server.Api.Services;

/// <summary>
/// Thread-safe tracker for which peers are currently editing which documents.
/// </summary>
/// <remarks>
/// Uses concurrent collections for lock-free reads and safe concurrent writes.
/// Maintains a reverse index (peer→documents) for efficient disconnect cleanup.
/// </remarks>
[PublicAPI]
public sealed class DocumentPeerTracker
{
  // documentId → (peerId → PeerInfoDto)
  readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PeerInfoDto>> _documentPeers = new();

  // peerId → set of documentIds (reverse index for disconnect cleanup)
  readonly ConcurrentDictionary<string, HashSet<string>> _peerDocuments = new();

  // Lock for the reverse-index HashSet mutations (HashSet is not thread-safe)
  readonly object _reverseIndexLock = new();

  /// <summary>
  /// Registers a peer as editing a document.
  /// </summary>
  /// <param name="documentId">The document ID.</param>
  /// <param name="peerInfo">The peer information.</param>
  public void AddPeerToDocument(string documentId, PeerInfoDto peerInfo)
  {
    var peers = _documentPeers.GetOrAdd(documentId, _ => new ConcurrentDictionary<string, PeerInfoDto>());
    peers[peerInfo.PeerId] = peerInfo;

    lock(_reverseIndexLock)
    {
      var docs = _peerDocuments.GetOrAdd(peerInfo.PeerId, _ => []);
      docs.Add(documentId);
    }
  }

  /// <summary>
  /// Removes a peer from a specific document.
  /// </summary>
  /// <param name="documentId">The document ID.</param>
  /// <param name="peerId">The peer ID to remove.</param>
  public void RemovePeerFromDocument(string documentId, string peerId)
  {
    if(_documentPeers.TryGetValue(documentId, out var peers))
    {
      peers.TryRemove(peerId, out _);
    }

    lock(_reverseIndexLock)
    {
      if(_peerDocuments.TryGetValue(peerId, out var docs))
      {
        docs.Remove(documentId);
        if(docs.Count == 0)
          _peerDocuments.TryRemove(peerId, out _);
      }
    }
  }

  /// <summary>
  /// Removes a peer from all documents it was editing (disconnect cleanup).
  /// </summary>
  /// <param name="peerId">The peer ID to remove.</param>
  /// <returns>The list of document IDs the peer was removed from.</returns>
  public List<string> RemovePeerFromAllDocuments(string peerId)
  {
    List<string> affectedDocuments;

    lock(_reverseIndexLock)
    {
      if(!_peerDocuments.TryRemove(peerId, out var docs))
        return [];

      affectedDocuments = [.. docs];
    }

    foreach(var documentId in affectedDocuments)
    {
      if(_documentPeers.TryGetValue(documentId, out var peers))
      {
        peers.TryRemove(peerId, out _);
      }
    }

    return affectedDocuments;
  }

  /// <summary>
  /// Gets all peers currently editing a document.
  /// </summary>
  /// <param name="documentId">The document ID.</param>
  /// <param name="excludePeerId">Optional peer ID to exclude from the result.</param>
  /// <returns>A list of peer info DTOs.</returns>
  public List<PeerInfoDto> GetDocumentPeers(string documentId, string? excludePeerId = null)
  {
    if(!_documentPeers.TryGetValue(documentId, out var peers))
      return [];

    if(excludePeerId is null)
      return [.. peers.Values];

    return peers.Values
      .Where(p => p.PeerId != excludePeerId)
      .ToList();
  }

  /// <summary>
  /// Gets the count of peers currently editing a document.
  /// </summary>
  /// <param name="documentId">The document ID.</param>
  /// <param name="excludePeerId">Optional peer ID to exclude from the count.</param>
  /// <returns>The number of peers.</returns>
  public int GetDocumentPeerCount(string documentId, string? excludePeerId = null)
  {
    if(!_documentPeers.TryGetValue(documentId, out var peers))
      return 0;

    if(excludePeerId is null)
      return peers.Count;

    return peers.Values.Count(p => p.PeerId != excludePeerId);
  }
}
