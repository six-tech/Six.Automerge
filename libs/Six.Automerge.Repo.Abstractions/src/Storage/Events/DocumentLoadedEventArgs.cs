using Six.Automerge.Repository;

namespace Six.Automerge.Repository.Storage;

/// <summary>
/// Event args for document loaded events.
/// </summary>
[PublicAPI]
public sealed class DocumentLoadedEventArgs : EventArgs
{
  /// <summary>
  /// Gets the document identifier.
  /// </summary>
  public DocumentId DocumentId { get; }

  /// <summary>
  /// Initializes a new instance of <see cref="DocumentLoadedEventArgs"/>.
  /// </summary>
  /// <param name="documentId">The document identifier.</param>
  public DocumentLoadedEventArgs(DocumentId documentId)
  {
    DocumentId = documentId;
  }
}
