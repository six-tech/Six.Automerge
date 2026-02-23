// Copyright (c) Six. Licensed under the MIT License.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Six.Automerge.Repository;

namespace Six.Automerge.TextEdit;

/// <summary>
/// High-level wrapper around an Automerge text CRDT that provides
/// cached text/formatting state, debounced change application,
/// and observable streams for text and formatting changes.
/// </summary>
/// <remarks>
/// Uses Rx.NET pipelines with <c>Throttle</c> and <c>Select+Concat</c>
/// for debouncing and serialized async execution, replacing the earlier
/// Channel-based queue pattern.
/// </remarks>
[PublicAPI]
public sealed class TextDocument : IDisposable
{
  readonly DocHandle<object> _handle;
  readonly TextDocumentOptions _options;
  readonly AutomergeObjectId _textObjectId;
  readonly SemaphoreSlim _refreshLock = new(1, 1);

  readonly Subject<string> _localTextChanges = new();
  readonly Subject<Unit> _remoteRefreshes = new();
  readonly Subject<TextChange> _textChanged = new();
  readonly Subject<FormattingChange> _formattedTextChanged = new();
  readonly CompositeDisposable _subscriptions = new();

  string _cachedText = string.Empty;
  FormattedText _cachedFormattedText = new([]);
  int _localChangeDepth;
  bool _isDisposed;

  /// <summary>
  /// Observable stream of text changes (local and remote).
  /// </summary>
  public IObservable<TextChange> TextChanged => _textChanged;

  /// <summary>
  /// Observable stream of formatting changes (local and remote).
  /// </summary>
  public IObservable<FormattingChange> FormattedTextChanged => _formattedTextChanged;

  /// <summary>
  /// Gets the current plain text content.
  /// </summary>
  public string Text => Volatile.Read(ref _cachedText);

  /// <summary>
  /// Gets the current formatted text content.
  /// </summary>
  public FormattedText FormattedText => Volatile.Read(ref _cachedFormattedText);

  /// <summary>
  /// Gets the underlying document handle.
  /// </summary>
  public DocHandle<object> Handle => _handle;

  /// <summary>
  /// Gets the document ID.
  /// </summary>
  public string DocumentId => _handle.DocumentId;

  internal TextDocument(DocHandle<object> handle, AutomergeObjectId textObjectId, TextDocumentOptions options)
  {
    ArgumentNullException.ThrowIfNull(handle);
    ArgumentNullException.ThrowIfNull(textObjectId);
    ArgumentNullException.ThrowIfNull(options);

    _handle = handle;
    _textObjectId = textObjectId;
    _options = options;
  }

  /// <summary>
  /// Creates a new document with a text field and returns a wired <see cref="TextDocument"/>.
  /// </summary>
  public static async Task<TextDocument> CreateAsync(
    Repository.Repo repo,
    TextDocumentOptions? options = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(repo);
    options ??= new TextDocumentOptions();

    var handle = await repo.Create<object>();

    AutomergeObjectId textObjectId = default!;
    await handle.ChangeAsync(
      doc =>
      {
        textObjectId = doc.PutObject(doc.RootId, options.TextFieldKey, AutomergeContainerType.Text);
      },
      cancellationToken: cancellationToken);

    var textDoc = new TextDocument(handle, textObjectId, options);
    await textDoc.InitializeAsync(cancellationToken);
    return textDoc;
  }

  /// <summary>
  /// Opens an existing document, waits for it to be ready, and returns a wired <see cref="TextDocument"/>.
  /// Returns null if the document is not found within the timeout period.
  /// </summary>
  public static async Task<TextDocument?> OpenAsync(
    Repository.Repo repo,
    string documentId,
    TextDocumentOptions? options = null,
    TimeSpan? timeout = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(repo);
    ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
    options ??= new TextDocumentOptions();

    var handle = await repo.FindAsync<object>(documentId, timeout);
    if(handle is null)
      return null;

    var textObjectId = await handle.ReadAsync(
                         doc => doc.GetObject(doc.RootId, options.TextFieldKey),
                         cancellationToken);

    if(textObjectId is null)
      return null;

    var textDoc = new TextDocument(handle, textObjectId, options);
    await textDoc.InitializeAsync(cancellationToken);

    return textDoc;
  }

  /// <summary>
  /// Initializes the document by reading the current text and spans
  /// from the DocHandle, wiring Rx pipelines, and subscribing to change events.
  /// </summary>
  internal async Task InitializeAsync(CancellationToken cancellationToken = default)
  {
    await RefreshFromDocumentAsync(cancellationToken);

    IScheduler scheduler = _options.Scheduler ?? DefaultScheduler.Instance;

    // Local text change pipeline: debounce → serialize async FFI calls
    _subscriptions.Add(
      _localTextChanges
        .Throttle(TimeSpan.FromMilliseconds(_options.ChangeDebounceMs), scheduler)
        .Select(text => Observable.FromAsync(() => FlushLocalChangeAsync(text)))
        .Concat()
        .Subscribe());

    // Remote refresh pipeline: debounce → serialize async FFI calls
    _subscriptions.Add(
      _remoteRefreshes
        .Throttle(TimeSpan.FromMilliseconds(_options.RemoteRefreshDebounceMs), scheduler)
        .Select(_ => Observable.FromAsync(() => RefreshAndNotifyRemoteAsync()))
        .Concat()
        .Subscribe());

    _handle.Changed += OnDocHandleChanged;
  }

  /// <summary>
  /// Applies a text change by pushing the new text into the Rx pipeline.
  /// The pipeline debounces, diffs against cached text, and splices the CRDT.
  /// </summary>
  public Task ApplyTextChangeAsync(string newText, CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();
    
    _localTextChanges.OnNext(newText);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Applies a string mark to a range of text.
  /// </summary>
  public async Task ApplyMarkAsync(
    ulong start,
    ulong end,
    string markName,
    string markValue,
    ExpandMode expand = ExpandMode.After,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle
        .ChangeAsync(doc => doc.Mark(_textObjectId, start, end, markName, markValue, expand),
                     cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    await RefreshFromDocumentAsync(CancellationToken.None);
    _formattedTextChanged.OnNext(new FormattingChange(_cachedFormattedText, true));
  }

  /// <summary>
  /// Applies a boolean mark to a range of text.
  /// </summary>
  public async Task ApplyMarkAsync(
    ulong start,
    ulong end,
    string markName,
    bool markValue,
    ExpandMode expand = ExpandMode.After,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle
        .ChangeAsync(doc => doc.Mark(_textObjectId, start, end, markName, markValue, expand),
                     cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    await RefreshFromDocumentAsync(CancellationToken.None);
    _formattedTextChanged.OnNext(new FormattingChange(_cachedFormattedText, true));
  }

  /// <summary>
  /// Removes a mark from a range of text.
  /// </summary>
  public async Task RemoveMarkAsync(
    string markName,
    ulong start,
    ulong end,
    ExpandMode expand = ExpandMode.After,
    CancellationToken cancellationToken = default)
  {
    EnsureNotDisposed();

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle
        .ChangeAsync(doc => doc.Unmark(_textObjectId, markName, start, end, expand),
                     cancellationToken: cancellationToken);
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    await RefreshFromDocumentAsync(CancellationToken.None);
    _formattedTextChanged.OnNext(new FormattingChange(_cachedFormattedText, true));
  }

  public void Dispose()
  {
    if(_isDisposed)
    {
      return;
    }

    _isDisposed = true;
    _handle.Changed -= OnDocHandleChanged;

    _localTextChanges.OnCompleted();
    _remoteRefreshes.OnCompleted();
    _subscriptions.Dispose();

    _textChanged.OnCompleted();
    _formattedTextChanged.OnCompleted();

    _localTextChanges.Dispose();
    _remoteRefreshes.Dispose();
    _textChanged.Dispose();
    _formattedTextChanged.Dispose();
    _refreshLock.Dispose();
  }

  void OnDocHandleChanged(object? sender, DocHandleChangeEventArgs<object> e)
  {
    if(_isDisposed || Volatile.Read(ref _localChangeDepth) > 0)
      return;

    _remoteRefreshes.OnNext(Unit.Default);
  }

  async Task FlushLocalChangeAsync(string newText)
  {
    string currentText = _cachedText;
    var splice = TextDiff.ComputeSplice(currentText, newText);

    if(!splice.HasChanges)
    {
      return;
    }

    Interlocked.Increment(ref _localChangeDepth);
    try
    {
      await _handle.ChangeAsync(doc => doc.SpliceText(
                                  _textObjectId,
                                  (ulong)splice.Offset,
                                  splice.DeleteCount,
                                  splice.InsertText));
    }
    finally
    {
      Interlocked.Decrement(ref _localChangeDepth);
    }

    // Skip GetText FFI — we already know the text after local splice
    Volatile.Write(ref _cachedText, newText);
    await RefreshSpansOnlyAsync(CancellationToken.None);
    _textChanged.OnNext(new TextChange(_cachedText, true));
  }

  async Task RefreshAndNotifyRemoteAsync()
  {
    await RefreshFromDocumentAsync(CancellationToken.None);
    
    _textChanged.OnNext(new TextChange(_cachedText, false));
    _formattedTextChanged.OnNext(new FormattingChange(_cachedFormattedText, false));
  }

  async Task RefreshFromDocumentAsync(CancellationToken cancellationToken)
  {
    await _refreshLock.WaitAsync(cancellationToken);
    try
    {
      var textId = _textObjectId;

      var (text, spans) = await _handle.ReadAsync(
                            doc =>
                            {
                              string t = doc.GetText(textId);
                              var s = doc.GetSpans(textId);
                              return (t, s);
                            },
                            cancellationToken);

      Volatile.Write(ref _cachedText, text);
      Volatile.Write(ref _cachedFormattedText, FormattedText.FromSpans(spans));
    }
    finally
    {
      _refreshLock.Release();
    }
  }

  async Task RefreshSpansOnlyAsync(CancellationToken cancellationToken)
  {
    await _refreshLock.WaitAsync(cancellationToken);
    try
    {
      var spans = await _handle.ReadAsync(doc => doc.GetSpans(_textObjectId), cancellationToken);
      Volatile.Write(ref _cachedFormattedText, FormattedText.FromSpans(spans));
    }
    finally
    {
      _refreshLock.Release();
    }
  }

  void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);
}
