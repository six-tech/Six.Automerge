using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Event args for document saved events.
/// </summary>
[PublicAPI]
public sealed class DocumentSavedEventArgs : EventArgs
{
  /// <summary>
  /// Gets the document identifier.
  /// </summary>
  public DocumentId DocumentId { get; }

  /// <summary>
  /// Gets a value indicating whether storage was compacted during this save.
  /// </summary>
  public bool Compacted { get; }

  /// <summary>
  /// Initializes a new instance of <see cref="DocumentSavedEventArgs"/>.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  /// <param name="compacted">Whether storage was compacted.</param>
  public DocumentSavedEventArgs(DocumentId documentId, bool compacted)
  {
    DocumentId = documentId;
    Compacted = compacted;
  }
}
