// Copyright (c) Six. All rights reserved.

using Avalonia.Controls;
using Avalonia.Threading;
using DemoApp.CollaborativeEditor.ViewModels;
using DemoApp.Shared;
using DemoApp.Shared.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge.TextEdit;

namespace DemoApp.CollaborativeEditor.Views;

public partial class EditorWindow : Window
{
  readonly ILogger<EditorWindow> _logger;
  DocumentSessionDialogViewModel? _sessionVm;
  ConnectionStatusBarViewModel? _statusBarVm;
  Timer? _caretDebounceTimer;

  public EditorWindow()
  {
    var loggerFactory = App.Services?.GetService<ILoggerFactory>();
    _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EditorWindow>();
    InitializeComponent();
    Loaded += OnLoaded;
  }

  async void OnLoaded(object? sender, EventArgs e)
  {
    _sessionVm = new DocumentSessionDialogViewModel();
    _sessionVm.ResultReady += OnSessionResultReady;

    SessionDialog.DataContext = _sessionVm;
    SessionDialog.RefreshRequested += OnRefreshRequested;

    _statusBarVm = new ConnectionStatusBarViewModel();
    StatusBar.DataContext = _statusBarVm;

    // Track caret position with debounce (1.5s) to avoid per-pixel updates
    LocalEditor.PropertyChanged += (_, args) =>
    {
      if(args.Property == TextBox.CaretIndexProperty)
      {
        int index = LocalEditor.CaretIndex;
        _caretDebounceTimer?.Dispose();
        _caretDebounceTimer = new Timer(_ =>
          Dispatcher.UIThread.Post(() =>
          {
            if(DataContext is EditorViewModel v) v.CursorPosition = index;
          }), null, 1500, Timeout.Infinite);
      }
    };

    // Update selection length only on pointer/key release (not per-pixel during drag)
    LocalEditor.PointerReleased += (_, _) =>
    {
      if(DataContext is not EditorViewModel vm) return;
      vm.SelectionLength = Math.Abs(LocalEditor.SelectionEnd - LocalEditor.SelectionStart);
      vm.CursorPosition = LocalEditor.CaretIndex;
    };

    LocalEditor.KeyUp += (_, _) =>
    {
      if(DataContext is not EditorViewModel vm) return;
      vm.SelectionLength = Math.Abs(LocalEditor.SelectionEnd - LocalEditor.SelectionStart);
    };

    // Load stored documents, initialize connection, then fetch server documents
    if(DataContext is EditorViewModel vm2 && vm2.RepoService is not null)
    {
      _sessionVm.LoadStoredDocuments(vm2.RepoService.StorageDirectory);
      await vm2.RepoService.InitializeAsync();
      WireConnectionEvents(vm2.RepoService);
      await _sessionVm.LoadServerDocumentsAsync(vm2.RepoService);
      _sessionVm.SubscribeToDocumentsChanged(vm2.RepoService);
    }
  }

  async void OnRefreshRequested(object? sender, EventArgs e)
  {
    if(_sessionVm is null || DataContext is not EditorViewModel vm || vm.RepoService is null)
    {
      return;
    }

    await _sessionVm.LoadServerDocumentsAsync(vm.RepoService);
  }

  async void OnSessionResultReady(object? sender, EventArgs e)
  {
    if(_sessionVm?.Result is null || DataContext is not EditorViewModel vm)
    {
      return;
    }

    var result = _sessionVm.Result;
    _sessionVm.IsLoading = true;
    _sessionVm.ErrorMessage = null;

    try
    {
      switch(result.Action)
      {
        case SessionAction.Create:
          await HandleCreateAsync(vm);
          break;
        case SessionAction.Join:
          await HandleJoinAsync(vm, result.DocumentId!);
          break;
        case SessionAction.Resume:
          await HandleResumeAsync(vm, result.DocumentId!);
          break;
      }

      await Dispatcher.UIThread.InvokeAsync(() => _sessionVm.IsLoading = false);
    }
    catch(OperationCanceledException ex)
    {
      _logger.LogWarning(ex, "Document {Action} was cancelled", result.Action);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.SyncStatus = "Operation cancelled";
        _sessionVm.IsLoading = false;
      });
    }
    catch(TimeoutException ex)
    {
      _logger.LogWarning(ex, "Document {Action} timed out", result.Action);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.SyncStatus = "Operation timed out";
        _sessionVm.ErrorMessage = "The operation timed out. The document may be unavailable or the server is not responding.";
        _sessionVm.IsLoading = false;
      });
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "Document {Action} failed", result.Action);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.SyncStatus = $"Error: {ex.Message}";
        _sessionVm.ErrorMessage = ex.Message;
        _sessionVm.IsLoading = false;
      });
    }
  }

  async Task HandleCreateAsync(EditorViewModel vm)
  {
    if(vm.RepoService is null)
    {
      _logger.LogInformation("Creating offline document (no RepoService)");
      await Dispatcher.UIThread.InvokeAsync(() => vm.SyncStatus = "Creating...");

      // For offline mode, create a standalone repo with in-memory adapters
      var repo = new Six.Automerge.Repository.Repo(new Six.Automerge.Repository.RepoConfig());
      TextDocument textDoc = await TextDocument.CreateAsync(repo);

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.SetTextDocument(textDoc);
        vm.SyncStatus = "Ready (offline)";
      });
      await ShowEditorAsync(null);
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => vm.SyncStatus = "Creating...");
    await vm.RepoService.InitializeAsync();
    WireConnectionEvents(vm.RepoService);

    TextDocument textDocument = await TextDocument.CreateAsync(vm.RepoService.Repo!);

    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      vm.SetTextDocument(textDocument);
      vm.SyncStatus = "Ready";
    });

    _logger.LogInformation("Created document {DocumentId}", textDocument.DocumentId);
    await ShowEditorAsync(textDocument.DocumentId);
    await FetchInitialDocumentPeersAsync(vm.RepoService, vm, textDocument.DocumentId);
  }

  async Task HandleJoinAsync(EditorViewModel vm, string documentId)
  {
    if(vm.RepoService is null)
    {
      _logger.LogWarning("Cannot join document {DocumentId}: no server connection", documentId);
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Cannot join: no server connection");
      return;
    }

    await vm.RepoService.InitializeAsync();
    WireConnectionEvents(vm.RepoService);

    await Dispatcher.UIThread.InvokeAsync(() => vm.SyncStatus = "Joining...");
    await Dispatcher.UIThread.InvokeAsync(() => vm.SyncStatus = "Syncing...");

    TextDocument? textDocument = await TextDocument.OpenAsync(
      vm.RepoService.Repo!,
      documentId,
      timeout: TimeSpan.FromSeconds(30));

    if(textDocument is null)
    {
      _logger.LogWarning("Join failed: document {DocumentId} not found", documentId);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        _sessionVm!.ErrorMessage = "Document not found";
        vm.SyncStatus = "Disconnected";
      });
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      vm.SetTextDocument(textDocument);
      vm.SyncStatus = "Ready";
    });

    _logger.LogInformation("Joined document {DocumentId}", textDocument.DocumentId);
    await ShowEditorAsync(textDocument.DocumentId);
    await FetchInitialDocumentPeersAsync(vm.RepoService, vm, textDocument.DocumentId);
  }

  async Task HandleResumeAsync(EditorViewModel vm, string documentId)
  {
    if(vm.RepoService is null)
    {
      _logger.LogWarning("Cannot resume document {DocumentId}: no server connection", documentId);
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Cannot resume: no server connection");
      return;
    }

    await vm.RepoService.InitializeAsync();
    WireConnectionEvents(vm.RepoService);

    await Dispatcher.UIThread.InvokeAsync(() => vm.SyncStatus = "Resuming...");

    TextDocument? textDocument = await TextDocument.OpenAsync(
      vm.RepoService.Repo!,
      documentId,
      timeout: TimeSpan.FromSeconds(10));

    if(textDocument is null)
    {
      _logger.LogWarning("Resume failed: document {DocumentId} not found in storage", documentId);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        _sessionVm!.ErrorMessage = "Document not found in storage";
        vm.SyncStatus = "Disconnected";
      });
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      vm.SetTextDocument(textDocument);
      vm.SyncStatus = "Ready";
    });

    _logger.LogInformation("Resumed document {DocumentId}", textDocument.DocumentId);
    await ShowEditorAsync(textDocument.DocumentId);
    await FetchInitialDocumentPeersAsync(vm.RepoService, vm, textDocument.DocumentId);
  }

  void WireConnectionEvents(RepoService repoService)
  {
    if(_statusBarVm is null)
    {
      return;
    }

    _statusBarVm.PeerId = repoService.PeerId?.ToString();
    _statusBarVm.ServerUrl = repoService is { IsInitialized: true }
      ? "Connected"
      : "Offline";

    // Set initial connection state (event handler below only fires on future changes)
    DemoApp.Shared.ConnectionState MapState(ConnectionState s) => s switch
    {
      ConnectionState.Connected => DemoApp.Shared.ConnectionState.Connected,
      ConnectionState.Connecting => DemoApp.Shared.ConnectionState.Connecting,
      ConnectionState.Reconnecting => DemoApp.Shared.ConnectionState.Reconnecting,
      _ => DemoApp.Shared.ConnectionState.Disconnected
    };

    _statusBarVm.ConnectionState = MapState(repoService.ConnectionState);

    repoService.ConnectionStateChanged += (_, state) =>
      Dispatcher.UIThread.Post(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.ConnectionState = MapState(state);
        }

        if(DataContext is EditorViewModel vm && vm.TextDocument is not null)
        {
          vm.UpdateConnectionStatus(MapState(state), repoService.DocumentPeerCount);
        }
      });

    repoService.DocumentPeerCountChanged += (_, count) =>
      Dispatcher.UIThread.Post(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.PeerCount = count;
        }

        if(DataContext is EditorViewModel vm && vm.TextDocument is not null)
        {
          vm.UpdateConnectionStatus(MapState(repoService.ConnectionState), count);
        }
      });
  }

  async Task FetchInitialDocumentPeersAsync(RepoService repoService, EditorViewModel vm, string documentId)
  {
    try
    {
      var peers = await repoService.GetDocumentPeersAsync(documentId);
      int count = peers.Count + 1; // +1 to include self
      _logger.LogInformation("Initial document peers for {DocumentId}: {PeerCount}", documentId, count);

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.PeerCount = count;
        }

        DemoApp.Shared.ConnectionState MapState(ConnectionState s) => s switch
        {
          ConnectionState.Connected => DemoApp.Shared.ConnectionState.Connected,
          ConnectionState.Connecting => DemoApp.Shared.ConnectionState.Connecting,
          ConnectionState.Reconnecting => DemoApp.Shared.ConnectionState.Reconnecting,
          _ => DemoApp.Shared.ConnectionState.Disconnected
        };

        vm.UpdateConnectionStatus(MapState(repoService.ConnectionState), count);
      });
    }
    catch(Exception ex)
    {
      _logger.LogWarning(ex, "Failed to fetch initial document peers for {DocumentId}", documentId);
    }
  }

  async Task ShowEditorAsync(string? documentId)
  {
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      SessionPanel.IsVisible = false;
      EditorPanel.IsVisible = true;

      if(_statusBarVm is not null)
        _statusBarVm.DocumentId = documentId ?? "local";

      LocalEditor.Focus();
    });
  }

  protected override void OnClosed(EventArgs e)
  {
    _caretDebounceTimer?.Dispose();
    base.OnClosed(e);
  }

}
