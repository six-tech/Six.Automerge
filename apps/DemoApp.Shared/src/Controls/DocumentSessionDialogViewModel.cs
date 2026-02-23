// Copyright (c) Six. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DemoApp.Shared.Controls;

/// <summary>
/// ViewModel for the <see cref="DocumentSessionDialog"/> control.
/// </summary>
[PublicAPI]
public sealed class DocumentSessionDialogViewModel : INotifyPropertyChanged
{
  RepoService? _repoService;
  string _joinDocumentId = string.Empty;
  string? _errorMessage;
  SessionResult? _result;
  bool _isLoading;
  bool _isLoadingServerDocuments;

  /// <inheritdoc />
  public event PropertyChangedEventHandler? PropertyChanged;

  /// <summary>
  /// Raised when the user has made a selection and the dialog should close.
  /// </summary>
  public event EventHandler? ResultReady;

  /// <summary>
  /// Gets or sets the document ID to join.
  /// </summary>
  public string JoinDocumentId
  {
    get => _joinDocumentId;
    set
    {
      if(_joinDocumentId == value)
      {
        return;
      }

      _joinDocumentId = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanJoin));
    }
  }

  /// <summary>
  /// Gets whether the Join button should be enabled.
  /// </summary>
  public bool CanJoin => !string.IsNullOrWhiteSpace(_joinDocumentId) && !_isLoading;

  /// <summary>
  /// Gets or sets whether an async session operation is in progress.
  /// </summary>
  public bool IsLoading
  {
    get => _isLoading;
    set
    {
      if(_isLoading == value)
      {
        return;
      }

      _isLoading = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanJoin));
    }
  }

  /// <summary>
  /// Gets or sets an error message to display. Null when no error.
  /// </summary>
  public string? ErrorMessage
  {
    get => _errorMessage;
    set
    {
      if(_errorMessage == value)
      {
        return;
      }

      _errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  /// <summary>
  /// Gets whether there is an error to display.
  /// </summary>
  public bool HasError => !string.IsNullOrEmpty(_errorMessage);

  /// <summary>
  /// Gets or sets whether server documents are currently being loaded.
  /// </summary>
  public bool IsLoadingServerDocuments
  {
    get => _isLoadingServerDocuments;
    set
    {
      if(_isLoadingServerDocuments == value)
      {
        return;
      }

      _isLoadingServerDocuments = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  /// Gets the list of previously stored document IDs (local).
  /// </summary>
  public ObservableCollection<string> StoredDocuments { get; } = [];

  /// <summary>
  /// Gets the list of server document IDs.
  /// </summary>
  public ObservableCollection<string> ServerDocuments { get; } = [];

  /// <summary>
  /// Gets the merged list of all documents from both local and server sources.
  /// </summary>
  public ObservableCollection<DocumentEntry> AllDocuments { get; } = [];

  /// <summary>
  /// Gets the dialog result after the user makes a selection.
  /// </summary>
  public SessionResult? Result
  {
    get => _result;
    private set
    {
      _result = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  /// Creates a new document.
  /// </summary>
  public void CreateNew()
  {
    Result = new SessionResult { Action = SessionAction.Create };
    ResultReady?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>
  /// Joins an existing document using the entered document ID.
  /// </summary>
  public void Join()
  {
    if(!CanJoin)
    {
      return;
    }

    Result = new SessionResult
    {
      Action = SessionAction.Join,
      DocumentId = _joinDocumentId.Trim()
    };

    ResultReady?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>
  /// Resumes a previously stored document.
  /// </summary>
  /// <param name="documentId">The stored document ID to resume.</param>
  public void Resume(string documentId)
  {
    Result = new SessionResult
    {
      Action = SessionAction.Resume,
      DocumentId = documentId
    };

    ResultReady?.Invoke(this, EventArgs.Empty);
  }

  /// <summary>
  /// Scans the storage directory for previously stored documents.
  /// Uses the FileSystemStorageAdapter's sharded directory structure.
  /// </summary>
  /// <param name="storageDirectory">The base storage directory to scan.</param>
  public void LoadStoredDocuments(string storageDirectory)
  {
    StoredDocuments.Clear();

    if(!Directory.Exists(storageDirectory))
    {
      return;
    }

    // FileSystemStorageAdapter sharding: first 2 chars are the shard dir,
    // remaining chars are the subdirectory name.
    // Reconstruct document IDs by combining shard prefix + subdirectory name.
    foreach(string shardDir in Directory.GetDirectories(storageDirectory))
    {
      string shardName = Path.GetFileName(shardDir);
      if(shardName.Length != 2)
      {
        continue;
      }

      foreach(string docDir in Directory.GetDirectories(shardDir))
      {
        string docSuffix = Path.GetFileName(docDir);
        string documentId = shardName + docSuffix;
        StoredDocuments.Add(documentId);
      }
    }

    RefreshAllDocuments();
  }

  /// <summary>
  /// Loads document IDs from the server via the <see cref="RepoService"/> and merges them
  /// with local documents. Handles connection failures gracefully by showing only local documents.
  /// </summary>
  /// <param name="repoService">The repo service to query. If null or not connected, only local documents are shown.</param>
  public async Task LoadServerDocumentsAsync(RepoService? repoService)
  {
    IsLoadingServerDocuments = true;

    try
    {
      ServerDocuments.Clear();

      if(repoService is null || repoService.ConnectionState != ConnectionState.Connected)
      {
        RefreshAllDocuments();
        return;
      }

      try
      {
        List<string> serverDocs = await repoService.ListServerDocumentsAsync();
        foreach(string docId in serverDocs)
        {
          ServerDocuments.Add(docId);
        }
      }
      catch(Exception)
      {
        // Connection failure — show only local documents
        ServerDocuments.Clear();
      }

      RefreshAllDocuments();
    }
    finally
    {
      IsLoadingServerDocuments = false;
    }
  }

  /// <summary>
  /// Subscribes to <see cref="RepoService.DocumentsChanged"/> so the document list
  /// auto-refreshes when the server notifies about new documents.
  /// </summary>
  /// <param name="repoService">The repo service to subscribe to.</param>
  public void SubscribeToDocumentsChanged(RepoService repoService)
  {
    ArgumentNullException.ThrowIfNull(repoService);

    UnsubscribeFromDocumentsChanged();

    _repoService = repoService;
    _repoService.DocumentsChanged += OnDocumentsChanged;
  }

  /// <summary>
  /// Unsubscribes from a previously subscribed <see cref="RepoService.DocumentsChanged"/> event.
  /// </summary>
  public void UnsubscribeFromDocumentsChanged()
  {
    if(_repoService is not null)
    {
      _repoService.DocumentsChanged -= OnDocumentsChanged;
      _repoService = null;
    }
  }

  /// <summary>
  /// Rebuilds the <see cref="AllDocuments"/> collection by merging
  /// <see cref="StoredDocuments"/> and <see cref="ServerDocuments"/>.
  /// </summary>
  public void RefreshAllDocuments()
  {
    AllDocuments.Clear();

    HashSet<string> localSet = new(StoredDocuments, StringComparer.Ordinal);
    HashSet<string> serverSet = new(ServerDocuments, StringComparer.Ordinal);
    HashSet<string> seen = new(StringComparer.Ordinal);

    // Add local documents first
    foreach(string docId in StoredDocuments)
    {
      if(!seen.Add(docId))
      {
        continue;
      }

      DocumentSource source = serverSet.Contains(docId) ? DocumentSource.Both : DocumentSource.Local;
      AllDocuments.Add(new DocumentEntry(docId, source));
    }

    // Add server-only documents
    foreach(string docId in ServerDocuments)
    {
      if(!seen.Add(docId))
      {
        continue;
      }

      AllDocuments.Add(new DocumentEntry(docId, DocumentSource.Server));
    }

    OnPropertyChanged(nameof(AllDocuments));
  }

  async void OnDocumentsChanged(object? sender, EventArgs e)
  {
    if(_repoService is not null)
    {
      await LoadServerDocumentsAsync(_repoService);
    }
  }

  void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
