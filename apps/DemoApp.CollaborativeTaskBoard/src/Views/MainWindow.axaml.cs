// Copyright (c) Six. All rights reserved.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DemoApp.Shared;
using DemoApp.Shared.Controls;
using DemoApp.CollaborativeTaskBoard.ViewModels;
using Six.Automerge.TaskBoard;
using Six.Automerge.Repository;

namespace DemoApp.CollaborativeTaskBoard.Views;

public partial class MainWindow : Window
{
  DocumentSessionDialogViewModel? _sessionVm;
  ConnectionStatusBarViewModel? _statusBarVm;

  public MainWindow()
  {
    InitializeComponent();
    Loaded += OnLoaded;
  }

  BoardViewModel? ViewModel => DataContext as BoardViewModel;

  async void OnLoaded(object? sender, EventArgs e)
  {
    _sessionVm = new DocumentSessionDialogViewModel();
    _sessionVm.ResultReady += OnSessionResultReady;

    SessionDialog.DataContext = _sessionVm;
    SessionDialog.RefreshRequested += OnRefreshRequested;

    _statusBarVm = new ConnectionStatusBarViewModel();
    StatusBar.DataContext = _statusBarVm;

    // Load stored documents, initialize connection, then fetch server documents
    if(ViewModel?.RepoService is not null)
    {
      _sessionVm.LoadStoredDocuments(ViewModel.RepoService.StorageDirectory);
      await ViewModel.RepoService.InitializeAsync();
      WireConnectionEvents(ViewModel.RepoService);
      await _sessionVm.LoadServerDocumentsAsync(ViewModel.RepoService);
      _sessionVm.SubscribeToDocumentsChanged(ViewModel.RepoService);
    }
  }

  async void OnRefreshRequested(object? sender, EventArgs e)
  {
    if(_sessionVm is null || ViewModel?.RepoService is null)
    {
      return;
    }

    await _sessionVm.LoadServerDocumentsAsync(ViewModel.RepoService);
  }

  async void OnSessionResultReady(object? sender, EventArgs e)
  {
    if(_sessionVm?.Result is null || ViewModel is null)
    {
      return;
    }

    var result = _sessionVm.Result;
    var vm = ViewModel;
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
    catch(OperationCanceledException)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.StatusText = "Operation cancelled";
        _sessionVm.IsLoading = false;
      });
    }
    catch(TimeoutException)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.StatusText = "Operation timed out";
        _sessionVm.ErrorMessage = "The operation timed out. The document may be unavailable or the server is not responding.";
        _sessionVm.IsLoading = false;
      });
    }
    catch(Exception ex)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.StatusText = $"Error: {ex.Message}";
        _sessionVm.ErrorMessage = ex.Message;
        _sessionVm.IsLoading = false;
      });
    }
  }

  async Task HandleCreateAsync(BoardViewModel vm)
  {
    if(vm.RepoService is null)
    {
      // Offline mode: create a standalone document
      var repo = new Repo(new RepoConfig());
      var doc = await TaskBoardDocument.CreateAsync(repo);
      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        vm.SetTaskBoardDocument(doc);
        vm.SeedAndRefresh();
      });

      await ShowEditorAsync(null);
      return;
    }

    await vm.RepoService.InitializeAsync();
    WireConnectionEvents(vm.RepoService);

    var document = await TaskBoardDocument.CreateAsync(vm.RepoService.Repo!);
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      vm.SetTaskBoardDocument(document);
      vm.SeedAndRefresh();
    });

    await ShowEditorAsync(document.DocumentId);
    await FetchInitialDocumentPeersAsync(vm.RepoService, document.DocumentId);
  }

  async Task HandleJoinAsync(BoardViewModel vm, string documentId)
  {
    if(vm.RepoService is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Cannot join: no server connection");
      return;
    }

    await vm.RepoService.InitializeAsync();
    WireConnectionEvents(vm.RepoService);

    await Dispatcher.UIThread.InvokeAsync(() => vm.StatusText = "Joining board...");

    var doc = await TaskBoardDocument.OpenAsync(
      vm.RepoService.Repo!, documentId, timeout: TimeSpan.FromSeconds(30));
    if(doc is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Board not found");
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => vm.SetTaskBoardDocument(doc));

    await ShowEditorAsync(doc.DocumentId);
    await FetchInitialDocumentPeersAsync(vm.RepoService, doc.DocumentId);
  }

  async Task HandleResumeAsync(BoardViewModel vm, string documentId)
  {
    if(vm.RepoService is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Cannot resume: no server connection");
      return;
    }

    await vm.RepoService.InitializeAsync();
    WireConnectionEvents(vm.RepoService);

    await Dispatcher.UIThread.InvokeAsync(() => vm.StatusText = "Resuming board...");

    var doc = await TaskBoardDocument.OpenAsync(
      vm.RepoService.Repo!, documentId, timeout: TimeSpan.FromSeconds(10));
    if(doc is null)
    {
      await Dispatcher.UIThread.InvokeAsync(() =>
        _sessionVm!.ErrorMessage = "Board not found in storage");
      return;
    }

    await Dispatcher.UIThread.InvokeAsync(() => vm.SetTaskBoardDocument(doc));

    await ShowEditorAsync(doc.DocumentId);
    await FetchInitialDocumentPeersAsync(vm.RepoService, doc.DocumentId);
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
    _statusBarVm.ConnectionState = repoService.ConnectionState switch
    {
      ConnectionState.Connected => DemoApp.Shared.ConnectionState.Connected,
      ConnectionState.Connecting => DemoApp.Shared.ConnectionState.Connecting,
      ConnectionState.Reconnecting => DemoApp.Shared.ConnectionState.Reconnecting,
      _ => DemoApp.Shared.ConnectionState.Disconnected
    };

    repoService.ConnectionStateChanged += (_, state) =>
      Dispatcher.UIThread.Post(() =>
      {
        if(_statusBarVm is null)
        {
          return;
        }

        _statusBarVm.ConnectionState = state switch
        {
          ConnectionState.Connected => DemoApp.Shared.ConnectionState.Connected,
          ConnectionState.Connecting => DemoApp.Shared.ConnectionState.Connecting,
          ConnectionState.Reconnecting => DemoApp.Shared.ConnectionState.Reconnecting,
          _ => DemoApp.Shared.ConnectionState.Disconnected
        };
      });

    repoService.DocumentPeerCountChanged += (_, count) =>
      Dispatcher.UIThread.Post(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.PeerCount = count;
        }
      });
  }

  async Task FetchInitialDocumentPeersAsync(RepoService repoService, string documentId)
  {
    try
    {
      var peers = await repoService.GetDocumentPeersAsync(documentId);
      int count = peers.Count + 1; // +1 to include self

      await Dispatcher.UIThread.InvokeAsync(() =>
      {
        if(_statusBarVm is not null)
        {
          _statusBarVm.PeerCount = count;
        }
      });
    }
    catch
    {
      // Non-critical — peer count will update via events
    }
  }

  async Task ShowEditorAsync(string? documentId)
  {
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      SessionPanel.IsVisible = false;
      EditorPanel.IsVisible = true;

      if(_statusBarVm is not null)
      {
        _statusBarVm.DocumentId = documentId ?? "local";
      }
    });
  }

  void OnAddTaskClick(object? sender, RoutedEventArgs e)
  {
    if(sender is Button { Tag: ColumnViewModel column } && ViewModel is not null)
    {
      string title = $"Task {column.Tasks.Count + 1}";
      ViewModel.AddTask(column, title);
    }
  }

  void OnRemoveColumnClick(object? sender, RoutedEventArgs e)
  {
    if(sender is Button { Tag: ColumnViewModel column } && ViewModel is not null)
    {
      ViewModel.RemoveColumn(column);
    }
  }

  void OnRemoveTaskClick(object? sender, RoutedEventArgs e)
  {
    if(sender is not Button { Tag: TaskItemViewModel task } || ViewModel is null)
    {
      return;
    }

    // Find which column contains this task
    foreach(var column in ViewModel.Columns)
    {
      if(column.Tasks.Contains(task))
      {
        ViewModel.RemoveTask(column, task);
        break;
      }
    }
  }

  void OnUpvoteClick(object? sender, RoutedEventArgs e)
  {
    if(sender is Button { Tag: TaskItemViewModel task } && ViewModel is not null)
    {
      ViewModel.UpvoteTask(task);
    }
  }
}
