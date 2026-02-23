// Copyright (c) Six. All rights reserved.

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Six.Automerge.Repo.Network.SignalR;
using Six.Automerge.Repo.Network.SignalR.Client;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.Repository.Network.SignalR;
using Six.Automerge.Repository.Storage.FileSystem;

namespace DemoApp.Shared;

/// <summary>
/// Lifecycle manager for Repo + SignalRNetworkAdapter + FileSystemStorageAdapter.
/// Provides a single entry point for demo apps to set up collaborative document sync.
/// </summary>
[PublicAPI]
public sealed class RepoService : IDisposable
{
  readonly RepoServiceOptions _options;
  readonly ILoggerFactory _loggerFactory;
  readonly ILogger<RepoService> _logger;

  readonly HashSet<string> _knownDocumentIds = new(StringComparer.Ordinal);

  FileSystemStorageAdapter? _storage;
  SignalRConnectionService? _connectionService;
  SignalRNetworkAdapter? _networkAdapter;
  Repo? _repo;
  HubConnection? _hubConnection;
  ConnectionState _connectionState = ConnectionState.Disconnected;
  int _peerCount;
  int _documentPeerCount;
  bool _disposed;

  /// <summary>
  /// Initializes a new instance of the <see cref="RepoService"/> class.
  /// </summary>
  /// <param name="options">Configuration options.</param>
  /// <param name="loggerFactory">Optional logger factory. Uses <see cref="NullLoggerFactory"/> if null.</param>
  public RepoService(RepoServiceOptions options, ILoggerFactory? loggerFactory = null)
  {
    ArgumentNullException.ThrowIfNull(options);
    _options = options;
    _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    _logger = _loggerFactory.CreateLogger<RepoService>();
  }

  /// <summary>
  /// Gets the peer ID of this repo instance. Null before initialization.
  /// </summary>
  public PeerId? PeerId => _repo?.PeerId;

  /// <summary>
  /// Gets the current connection state to the SignalR hub.
  /// </summary>
  public ConnectionState ConnectionState => _connectionState;

  /// <summary>
  /// Gets the number of currently connected peers (Repo-level tracking).
  /// </summary>
  public int PeerCount => _peerCount;

  /// <summary>
  /// Gets the number of peers on the current document (server-reported, excludes self).
  /// </summary>
  public int DocumentPeerCount => _documentPeerCount;

  /// <summary>
  /// Gets the underlying <see cref="Repo"/> instance. Null before initialization.
  /// </summary>
  public Repo? Repo => _repo;

  /// <summary>
  /// Gets whether the service has been initialized.
  /// </summary>
  public bool IsInitialized => _repo is not null;

  /// <summary>
  /// Gets the underlying <see cref="Microsoft.AspNetCore.SignalR.Client.HubConnection"/>.
  /// Null before initialization.
  /// </summary>
  public HubConnection? HubConnection => _hubConnection;

  /// <summary>
  /// Gets the resolved hub URL. If <see cref="RepoServiceOptions.HubUrl"/> is set, returns it directly.
  /// Otherwise, resolves the base URL from Aspire service discovery env vars and appends
  /// <see cref="RepoServiceOptions.HubPath"/>.
  /// </summary>
  public string ResolvedHubUrl =>
    _options.HubUrl ?? $"{ResolveServerBaseUrl()}{_options.HubPath}";

  /// <summary>
  /// Gets the resolved storage directory path.
  /// When the <c>REPLICA_INDEX</c> environment variable is set, appends <c>/replica-{index}</c>
  /// to isolate per-replica storage.
  /// </summary>
  public string StorageDirectory
  {
    get
    {
      string baseDir = _options.StorageBaseDir
        ?? Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          ".rayautomerge",
          _options.AppName);

      string? replicaIndex = Environment.GetEnvironmentVariable("REPLICA_INDEX");
      if(!string.IsNullOrEmpty(replicaIndex))
      {
        return Path.Combine(baseDir, $"replica-{replicaIndex}");
      }

      return baseDir;
    }
  }

  /// <summary>
  /// Raised when the connection state changes.
  /// </summary>
  public event EventHandler<ConnectionState>? ConnectionStateChanged;

  /// <summary>
  /// Raised when the peer count changes.
  /// </summary>
  public event EventHandler<int>? PeerCountChanged;

  /// <summary>
  /// Raised when the document-level peer count changes (server-reported).
  /// The int value is the number of other peers on the document.
  /// </summary>
  public event EventHandler<int>? DocumentPeerCountChanged;

  /// <summary>
  /// Raised when the set of known documents changes (e.g., a new document becomes available on the server).
  /// </summary>
  public event EventHandler? DocumentsChanged;

  /// <summary>
  /// Initializes the repo, storage, and network adapter.
  /// Creates the storage directory, connects to the SignalR hub, and starts the repo.
  /// </summary>
  /// <param name="cancellationToken">Optional token to cancel the initialization.</param>
  public async Task InitializeAsync(CancellationToken cancellationToken = default)
  {
    ThrowIfDisposed();
    cancellationToken.ThrowIfCancellationRequested();

    if(_repo is not null)
    {
      _logger.LogWarning("RepoService is already initialized");
      return;
    }

    string resolvedUrl = ResolvedHubUrl;
    _logger.LogInformation("Initializing RepoService for app '{AppName}' with hub URL: {HubUrl}", _options.AppName, resolvedUrl);

    // Create file system storage
    string storageDir = StorageDirectory;
    Directory.CreateDirectory(storageDir);
    _storage = new FileSystemStorageAdapter(storageDir);
    _logger.LogInformation("Storage initialized at: {StorageDir}", storageDir);

    cancellationToken.ThrowIfCancellationRequested();

    // Create SignalR connection service
    IOptionsMonitor<SignalRConnectionServiceOptions> optionsMonitor =
      new StaticOptionsMonitor(new SignalRConnectionServiceOptions
      {
        HubUrl = resolvedUrl,
        AutoStart = true,
        EnableAutomaticReconnect = true,
        UseMessagePackProtocol = true
      });

    _connectionService = new SignalRConnectionService(_loggerFactory, optionsMonitor, string.Empty);

    // Create network adapter
    _networkAdapter = new SignalRNetworkAdapter(_connectionService, _loggerFactory);
    _networkAdapter.DocumentPeersChanged += OnDocumentPeersChanged;
    _networkAdapter.DocumentAvailable += OnDocumentAvailable;

    // Create repo
    _repo = new Repo(new RepoConfig
    {
      Storage = _storage,
      NetworkAdapters = [_networkAdapter]
    });

    _logger.LogInformation("Repo created with PeerId: {PeerId}", _repo.PeerId);

    // Wire up peer events
    _repo.PeerConnected += OnPeerConnected;
    _repo.PeerDisconnected += OnPeerDisconnected;

    // Connect the network adapter
    SetConnectionState(ConnectionState.Connecting);

    try
    {
      _networkAdapter.Connect(_repo.PeerId);
      await _networkAdapter.WhenReady().WaitAsync(_options.ConnectionTimeout, cancellationToken);

      // Capture the hub connection for RPC calls (e.g., ListServerDocumentsAsync)
      _hubConnection = await _connectionService.GetConnection();

      SetConnectionState(ConnectionState.Connected);
      _logger.LogInformation("Connected to SignalR hub: {HubUrl}", resolvedUrl);
    }
    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
    {
      _logger.LogWarning("RepoService initialization was cancelled");
      SetConnectionState(ConnectionState.Disconnected);
      throw;
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "Failed to connect to SignalR hub: {HubUrl}. Repo will work offline.", resolvedUrl);
      SetConnectionState(ConnectionState.Disconnected);
    }
  }

  /// <summary>
  /// Queries the server for the list of document IDs stored for this app's hub.
  /// Requires an active connection.
  /// </summary>
  /// <returns>A list of document ID strings.</returns>
  /// <exception cref="InvalidOperationException">Thrown when the service is not initialized or not connected.</exception>
  public async Task<List<string>> ListServerDocumentsAsync()
  {
    ThrowIfDisposed();

    if(_hubConnection is null || _connectionState != ConnectionState.Connected)
      throw new InvalidOperationException("Cannot list server documents: not connected to hub.");

    _logger.LogDebug("Requesting document list from server hub");
    return await _hubConnection.InvokeAsync<List<string>>("ListDocuments");
  }

  /// <summary>
  /// Queries the server for the list of peers currently on a document.
  /// </summary>
  /// <param name="documentId">The document to query.</param>
  /// <returns>A list of <see cref="PeerInfoDto"/> for other peers on the document.</returns>
  /// <exception cref="InvalidOperationException">Thrown when not connected.</exception>
  public async Task<List<PeerInfoDto>> GetDocumentPeersAsync(string documentId)
  {
    ThrowIfDisposed();

    if(_networkAdapter is null || _connectionState != ConnectionState.Connected)
      throw new InvalidOperationException("Cannot get document peers: not connected.");

    return await _networkAdapter.GetDocumentPeersAsync(documentId);
  }

  void OnDocumentAvailable(object? sender, Six.Automerge.Repo.Network.SignalR.DocumentAvailableEventArgs e)
  {
    bool added;
    lock(_knownDocumentIds)
    {
      added = _knownDocumentIds.Add(e.DocumentId);
    }

    if(added)
    {
      _logger.LogInformation("New document available from server: {DocumentId}", e.DocumentId);
      DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }
  }

  void OnDocumentPeersChanged(object? sender, DocumentPeersChangedEventArgs e)
  {
    int count = e.Peers.Count + 1; // +1 to include self
    int previous = Interlocked.Exchange(ref _documentPeerCount, count);

    if(previous != count)
    {
      _logger.LogInformation(
        "Document peer count changed: DocumentId={DocumentId}, PeerCount={PeerCount}",
        e.DocumentId, count);
      DocumentPeerCountChanged?.Invoke(this, count);
    }
  }

  void OnPeerConnected(object? sender, PeerEventArgs e)
  {
    int count = Interlocked.Increment(ref _peerCount);
    _logger.LogInformation("Peer connected: {PeerId}. Total peers: {PeerCount}", e.PeerId, count);
    PeerCountChanged?.Invoke(this, count);
  }

  void OnPeerDisconnected(object? sender, PeerEventArgs e)
  {
    int count = Interlocked.Add(ref _peerCount, -1);
    if(count < 0)
    {
      Interlocked.Exchange(ref _peerCount, 0);
      count = 0;
    }

    _logger.LogInformation("Peer disconnected: {PeerId}. Total peers: {PeerCount}", e.PeerId, count);
    PeerCountChanged?.Invoke(this, count);
  }

  void SetConnectionState(ConnectionState state)
  {
    if(_connectionState == state)
      return;

    _connectionState = state;
    _logger.LogInformation("Connection state changed to: {State}", state);
    ConnectionStateChanged?.Invoke(this, state);
  }

  /// <inheritdoc />
  public void Dispose()
  {
    if(_disposed)
    {
      return;
    }

    _disposed = true;

    if(_repo is not null)
    {
      _repo.PeerConnected -= OnPeerConnected;
      _repo.PeerDisconnected -= OnPeerDisconnected;
      _repo.Dispose();
    }

    if(_networkAdapter is not null)
    {
      _networkAdapter.DocumentPeersChanged -= OnDocumentPeersChanged;
      _networkAdapter.DocumentAvailable -= OnDocumentAvailable;
      _networkAdapter.Dispose();
    }

    _connectionService?.Dispose();
    _storage?.Dispose();
  }

  void ThrowIfDisposed()
  {
    ObjectDisposedException.ThrowIf(_disposed, this);
  }

  /// <summary>
  /// Resolves the server base URL from Aspire service discovery environment variables.
  /// Falls back to <c>https://localhost:5001</c> if no env vars are set.
  /// </summary>
  static string ResolveServerBaseUrl()
  {
    string? baseUrl = Environment.GetEnvironmentVariable("services__api__https__0")
      ?? Environment.GetEnvironmentVariable("services__api__http__0");

    if(!string.IsNullOrEmpty(baseUrl))
    {
      return baseUrl.TrimEnd('/');
    }

    // Fallback to HUB_BASE_URL env var or default
    string? hubBaseUrl = Environment.GetEnvironmentVariable("HUB_BASE_URL");
    if(!string.IsNullOrEmpty(hubBaseUrl))
    {
      return hubBaseUrl.TrimEnd('/');
    }

    return "https://localhost:5001";
  }

  
  
  /// <summary>
  /// Simple IOptionsMonitor implementation for creating SignalRConnectionService without DI.
  /// </summary>
  sealed class StaticOptionsMonitor(SignalRConnectionServiceOptions options)
    : IOptionsMonitor<SignalRConnectionServiceOptions>
  {
    public SignalRConnectionServiceOptions CurrentValue => options;

    public SignalRConnectionServiceOptions Get(string? name) => options;

    public IDisposable? OnChange(Action<SignalRConnectionServiceOptions, string?> listener) => null;
  }
}
