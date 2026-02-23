// Copyright (c) Six. All rights reserved.

using System.Reactive;
using System.Reactive.Disposables;
using DemoApp.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Six.Automerge;
using Six.Automerge.TextEdit;
using ReactiveUI;

namespace DemoApp.CollaborativeEditor.ViewModels;

public class EditorViewModel : ViewModelBase, IDisposable
{
  readonly RepoService? _repoService;
  readonly ILogger<EditorViewModel> _logger;
  readonly Action<Action> _dispatcher;
  TextDocument? _textDocument;
  CompositeDisposable? _textDocSubscriptions;
  string _documentText = string.Empty;
  string _syncStatus = "Disconnected";
  int _cursorPosition;
  int _selectionLength;
  FormattedText? _formattedText;
  bool _isBold;
  bool _isItalic;
  bool _isUnderline;
  int _isRemoteUpdate;
  bool _disposed;

  public EditorViewModel(
    RepoService? repoService = null,
    ILoggerFactory? loggerFactory = null,
    Action<Action>? dispatcher = null)
  {
    _repoService = repoService;
    _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<EditorViewModel>();
    _dispatcher = dispatcher ?? (action => action());

    SaveCommand = ReactiveCommand.CreateFromTask(SaveDocumentAsync);
    ExportJsonCommand = ReactiveCommand.CreateFromTask(ExportJsonAsync);
    ToggleBoldCommand = ReactiveCommand.CreateFromTask(ToggleBoldAsync);
    ToggleItalicCommand = ReactiveCommand.CreateFromTask(ToggleItalicAsync);
    ToggleUnderlineCommand = ReactiveCommand.CreateFromTask(ToggleUnderlineAsync);
  }

  /// <summary>
  /// The current text content of the local Automerge document.
  /// Two-way bound to the TextBox in the view.
  /// </summary>
  public string DocumentText
  {
    get => _documentText;
    set
    {
      if(_documentText == value)
        return;

      if(_textDocument is not null && Volatile.Read(ref _isRemoteUpdate) == 0)
      {
        _ = _textDocument.ApplyTextChangeAsync(value);
      }

      this.RaiseAndSetIfChanged(ref _documentText, value);
    }
  }

  /// <summary>
  /// Current sync status description.
  /// </summary>
  public string SyncStatus
  {
    get => _syncStatus;
    set => this.RaiseAndSetIfChanged(ref _syncStatus, value);
  }

  /// <summary>
  /// The current formatted text from the TextDocument, used for rich text rendering.
  /// </summary>
  public FormattedText? FormattedText
  {
    get => _formattedText;
    private set
    {
      this.RaiseAndSetIfChanged(ref _formattedText, value);
    }
  }

  /// <summary>
  /// Current cursor position in the local document.
  /// </summary>
  public int CursorPosition
  {
    get => _cursorPosition;
    set
    {
      this.RaiseAndSetIfChanged(ref _cursorPosition, value);
      UpdateFormattingState();
    }
  }

  /// <summary>
  /// Length of the current text selection.
  /// </summary>
  public int SelectionLength
  {
    get => _selectionLength;
    set => this.RaiseAndSetIfChanged(ref _selectionLength, value);
  }

  /// <summary>
  /// Whether bold formatting is active at the cursor.
  /// </summary>
  public bool IsBold
  {
    get => _isBold;
    set => this.RaiseAndSetIfChanged(ref _isBold, value);
  }

  /// <summary>
  /// Whether italic formatting is active at the cursor.
  /// </summary>
  public bool IsItalic
  {
    get => _isItalic;
    set => this.RaiseAndSetIfChanged(ref _isItalic, value);
  }

  /// <summary>
  /// Whether underline formatting is active at the cursor.
  /// </summary>
  public bool IsUnderline
  {
    get => _isUnderline;
    set => this.RaiseAndSetIfChanged(ref _isUnderline, value);
  }

  /// <summary>
  /// Gets the RepoService for document operations.
  /// </summary>
  internal RepoService? RepoService => _repoService;

  /// <summary>
  /// Gets the TextDocument for collaborative text operations.
  /// </summary>
  internal TextDocument? TextDocument => _textDocument;

  public ReactiveCommand<Unit, Unit> SaveCommand { get; }

  public ReactiveCommand<Unit, Unit> ExportJsonCommand { get; }

  public ReactiveCommand<Unit, Unit> ToggleBoldCommand { get; }

  public ReactiveCommand<Unit, Unit> ToggleItalicCommand { get; }

  public ReactiveCommand<Unit, Unit> ToggleUnderlineCommand { get; }

  /// <summary>
  /// Sets the TextDocument and subscribes to its observable streams.
  /// Called by EditorWindow after document creation/join/resume.
  /// </summary>
  internal void SetTextDocument(TextDocument textDocument)
  {
    _textDocSubscriptions?.Dispose();
    _textDocSubscriptions = new CompositeDisposable();

    _textDocument = textDocument;

    _textDocSubscriptions.Add(
      _textDocument.TextChanged.Subscribe(HandleTextChange));

    _textDocSubscriptions.Add(
      _textDocument.FormattedTextChanged.Subscribe(HandleFormattingChange));

    // Initialize UI text and formatted text from the document
    _documentText = _textDocument.Text;
    this.RaisePropertyChanged(nameof(DocumentText));
    FormattedText = _textDocument.FormattedText;
    
    UpdateFormattingState();
  }

  void HandleTextChange(TextChange change)
  {
    if(change.IsLocal)
    {
      // Local text change: the TextDocument cache is already refreshed.
      // Read the cached FormattedText without triggering a full RenderFormattedText.
      _dispatcher(() => FormattedText = _textDocument?.FormattedText);
      return;
    }

    _dispatcher(() =>
    {
      Interlocked.CompareExchange(ref _isRemoteUpdate, 1, 0);
      try
      {
        _logger.LogDebug("Remote update: refreshing document text ({CharCount} chars)", change.Text.Length);
        _documentText = change.Text;
        this.RaisePropertyChanged(nameof(DocumentText));
        FormattedText = _textDocument?.FormattedText;
      }
      finally
      {
        Interlocked.Exchange(ref _isRemoteUpdate, 0);
      }
    });
  }

  void HandleFormattingChange(FormattingChange change)
  {
    _dispatcher(() =>
    {
      FormattedText = change.FormattedText;
      UpdateFormattingState();
    });
  }

  /// <summary>
  /// Updates SyncStatus based on connection state and peer count.
  /// Called by EditorWindow when RepoService connection state or peer count changes.
  /// </summary>
  internal void UpdateConnectionStatus(ConnectionState connectionState, int peerCount)
  {
    SyncStatus = connectionState switch
    {
      ConnectionState.Connected when peerCount > 0 =>
        $"Syncing ({peerCount} {(peerCount == 1 ? "peer" : "peers")})",
      ConnectionState.Connected => "Ready",
      ConnectionState.Connecting => "Connecting...",
      ConnectionState.Reconnecting => "Reconnecting...",
      _ => "Disconnected"
    };
  }

  void UpdateFormattingState()
  {
    // if(_disposed || _textDocument is null)
    //   return;
    //
    // FormattedText? formattedText = FormattedText;
    // if(formattedText is null)
    //   return;
    //
    // FormattingState state = FormattingState.FromFormattedText(formattedText, _cursorPosition);
    // IsBold = state.IsBold;
    // IsItalic = state.IsItalic;
    // IsUnderline = state.IsUnderline;
  }

  async Task ToggleBoldAsync()
  {
    Console.Error.WriteLine($"[EditorViewModel] ToggleBoldAsync: textDoc={_textDocument is not null}, sel={_selectionLength}, pos={_cursorPosition}, isBold={_isBold}");
    
    if(_textDocument is null || _selectionLength <= 0)
      return;

    ulong start = (ulong)_cursorPosition;
    ulong end = (ulong)(_cursorPosition + _selectionLength);
    
    Console.Error.WriteLine($"[EditorViewModel] Applying mark: start={start}, end={end}, remove={_isBold}");

    if(_isBold)
    {
      await _textDocument.RemoveMarkAsync("bold", start, end, ExpandMode.Both);
    }
    else
    {
      await _textDocument.ApplyMarkAsync(start, end, "bold", true, ExpandMode.Both);
    }

    UpdateFormattingState();
  }

  async Task ToggleItalicAsync()
  {
    if(_textDocument is null || _selectionLength <= 0)
    {
      return;
    }

    ulong start = (ulong)_cursorPosition;
    ulong end = (ulong)(_cursorPosition + _selectionLength);

    if(_isItalic)
    {
      await _textDocument.RemoveMarkAsync("italic", start, end, ExpandMode.Both);
    }
    else
    {
      await _textDocument.ApplyMarkAsync(start, end, "italic", true, ExpandMode.Both);
    }

    UpdateFormattingState();
  }

  async Task ToggleUnderlineAsync()
  {
    if(_textDocument is null || _selectionLength <= 0)
    {
      return;
    }

    ulong start = (ulong)_cursorPosition;
    ulong end = (ulong)(_cursorPosition + _selectionLength);

    if(_isUnderline)
    {
      await _textDocument.RemoveMarkAsync("underline", start, end, ExpandMode.Both);
    }
    else
    {
      await _textDocument.ApplyMarkAsync(start, end, "underline", true, ExpandMode.Both);
    }

    UpdateFormattingState();
  }

  async Task SaveDocumentAsync()
  {
    if(_textDocument is null)
    {
      SyncStatus = "No document loaded";
      return;
    }

    byte[] saved = await _textDocument.Handle.ReadAsync(doc => doc.Save());
    SyncStatus = $"Saved ({saved.Length} bytes)";
  }

  async Task ExportJsonAsync()
  {
    if(_textDocument is null)
    {
      SyncStatus = "No document loaded";
      return;
    }

    string json = await _textDocument.Handle.ReadAsync(doc => doc.HydrateJson());
    
    _logger.LogInformation("Document JSON: {Json}", json);
    SyncStatus = $"Exported JSON ({json.Length} chars)";
  }

  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;

      _textDocSubscriptions?.Dispose();

      if(_textDocument is not null)
      {
        _textDocument.Dispose();
      }

      _repoService?.Dispose();
    }
  }
}
