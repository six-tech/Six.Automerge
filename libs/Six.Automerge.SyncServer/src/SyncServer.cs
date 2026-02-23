// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Concurrent;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Storage;
using AutomergeRepo = Six.Automerge.Repository.Repo;

namespace Six.Automerge.SyncServer;

/// <summary>
/// A server-side sync endpoint that manages an internal Repo for document storage
/// and synchronization. Uses a passive share config (does not announce, but accepts
/// all access) and bridges to an <see cref="IServerTransport"/> for external communication.
/// </summary>
[PublicAPI]
public sealed class SyncServer : IAsyncDisposable
{
  readonly ILogger _logger;
  readonly AutomergeRepo _repo;
  readonly TransportBridgeAdapter _bridgeAdapter;
  readonly DebouncedPersistence _persistence;
  readonly SyncServerOptions _options;
  readonly ConcurrentDictionary<string, byte> _knownDocumentIds = [];
  bool _disposed;

  /// <summary>
  /// Creates a new sync server with the specified options and transport.
  /// </summary>
  /// <param name="options">Server configuration options.</param>
  /// <param name="transport">The transport layer for external communication.</param>
  public SyncServer(SyncServerOptions options, IServerTransport transport)
  {
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(transport);

    _options = options;
    _logger = options.LoggerFactory?.CreateLogger<SyncServer>()
              ?? NullLoggerFactory.Instance.CreateLogger<SyncServer>();

    var peerId = PeerId.Generate();
    _bridgeAdapter = new TransportBridgeAdapter(transport, peerId);

    _repo = new AutomergeRepo(new RepoConfig
    {
      PeerId = peerId,
      Storage = options.Storage,
      NetworkAdapters = [_bridgeAdapter],
      SaveDebounceRate = options.SaveDebounceRate,
      SyncDebounceRate = options.SyncDebounceRate,
      ShareConfig = new ShareConfig
      {
        Announce = (_, _) => Task.FromResult(false),
        Access = (_, _) => Task.FromResult(true)
      },
      LoggerFactory = options.LoggerFactory
    });

    _persistence = new DebouncedPersistence(_repo, options.PersistenceInterval, options.LoggerFactory);

    _repo.DocumentLoaded += OnDocumentLoaded;
  }

  /// <summary>
  /// Raised when a document is first seen by the server (first message received for that document ID).
  /// </summary>
  public event EventHandler<DocumentAvailableEventArgs>? DocumentAvailable;

  /// <summary>
  /// The server's peer ID, used to identify the server in the sync protocol.
  /// </summary>
  public PeerId PeerId => _repo.PeerId;

  /// <summary>
  /// The internal Repo managed by this server. Exposed for advanced scenarios
  /// such as direct document manipulation.
  /// </summary>
  internal AutomergeRepo Repo => _repo;

  /// <summary>
  /// Called by the transport layer when a peer connects.
  /// </summary>
  /// <param name="peerId">The connecting peer's ID.</param>
  /// <param name="metadata">Optional peer metadata.</param>
  public Task OnPeerConnectedAsync(PeerId peerId, PeerMetadata? metadata = null)
  {
    ThrowIfDisposed();
    _logger.LogInformation("Peer connected: {PeerId}", peerId);
    _bridgeAdapter.RaisePeerConnected(peerId, metadata);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Called by the transport layer when a peer disconnects.
  /// </summary>
  /// <param name="peerId">The disconnecting peer's ID.</param>
  public Task OnPeerDisconnectedAsync(PeerId peerId)
  {
    ThrowIfDisposed();
    _logger.LogInformation("Peer disconnected: {PeerId}", peerId);
    _bridgeAdapter.RaisePeerDisconnected(peerId);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Called by the transport layer when a message is received from a peer.
  /// </summary>
  /// <param name="peerId">The sending peer's ID.</param>
  /// <param name="message">The received message.</param>
  public Task OnMessageReceivedAsync(PeerId peerId, IRepoMessage message)
  {
    ThrowIfDisposed();
    ArgumentNullException.ThrowIfNull(message);

    // Stamp the sender and relay to the internal Repo
    IRepoMessage stamped = message.WithSender(peerId);
    _bridgeAdapter.RaiseMessageReceived(stamped);

    // Track the document ID in-memory and mark dirty for periodic persistence
    if(message.DocumentId is { } documentId)
    {
      if(_knownDocumentIds.TryAdd(documentId.Value, 0))
      {
        DocumentAvailable?.Invoke(this, new DocumentAvailableEventArgs { DocumentId = documentId.Value });
      }

      _persistence.MarkDirty(documentId.Value);
    }

    return Task.CompletedTask;
  }

  /// <summary>
  /// Lists all document IDs currently stored in the server's storage.
  /// Scans storage for snapshot chunks and returns unique document IDs.
  /// </summary>
  /// <returns>A list of document IDs found in storage.</returns>
  public async Task<IReadOnlyList<string>> ListDocumentsAsync()
  {
    ThrowIfDisposed();

    // Start with in-memory tracked document IDs (immediately available)
    var documentIds = new HashSet<string>(_knownDocumentIds.Keys);

    // Also scan storage to catch documents from previous server runs
    try
    {
      var emptyPrefix = new StorageKey(Array.Empty<string>());
      var chunks = await _options.Storage.GetChunkInfo(emptyPrefix).ConfigureAwait(false);

      foreach(ChunkInfo chunk in chunks)
      {
        if(chunk.Key.Components is { Count: > 0 })
        {
          documentIds.Add(chunk.Key.Components[0]);
        }
      }
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "Failed to scan storage for documents; returning in-memory set only");
    }

    return documentIds.ToList();
  }

  /// <summary>
  /// Exports a document as binary data (Automerge save format).
  /// </summary>
  /// <param name="documentId">The document ID to export.</param>
  /// <returns>The binary document data, or null if not found.</returns>
  public async Task<byte[]?> ExportDocumentAsync(string documentId)
  {
    ThrowIfDisposed();
    return await _repo.ExportAsync(documentId).ConfigureAwait(false);
  }

  /// <summary>
  /// Deletes a document from the server, removing it from both cache and storage.
  /// </summary>
  /// <param name="documentId">The document ID to delete.</param>
  public async Task DeleteDocumentAsync(string documentId)
  {
    ThrowIfDisposed();
    _knownDocumentIds.TryRemove(documentId, out _);
    await _repo.DeleteAsync(documentId).ConfigureAwait(false);

    // Also clean up storage directly for documents not in cache
    var prefix = new StorageKey(new[] { documentId });
    await _options.Storage.RemoveRange(prefix).ConfigureAwait(false);
  }

  void OnDocumentLoaded(object? sender, DocumentEventArgs e)
  {
    _knownDocumentIds.TryAdd(e.DocumentId.Value, 0);
    _logger.LogDebug("Document loaded: {DocumentId}", e.DocumentId);
  }

  void ThrowIfDisposed()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
  }

  /// <summary>
  /// Shuts down the server, flushing all pending persistence and cleaning up resources.
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    if(_disposed) return;
    _disposed = true;

    _repo.DocumentLoaded -= OnDocumentLoaded;

    await _persistence.DisposeAsync().ConfigureAwait(false);
    await _repo.ShutdownAsync().ConfigureAwait(false);
  }
}
