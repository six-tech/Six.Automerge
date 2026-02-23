// Copyright (c) Six. All rights reserved.

namespace DemoApp.Shared.Controls;

/// <summary>
/// Represents a document in the merged document list with its source information.
/// </summary>
/// <param name="DocumentId">The document identifier.</param>
/// <param name="Source">Where this document originates from.</param>
[PublicAPI]
public sealed record DocumentEntry(string DocumentId, DocumentSource Source)
{
  /// <summary>
  /// Gets whether this document exists in local storage.
  /// </summary>
  public bool IsLocal => Source is DocumentSource.Local or DocumentSource.Both;

  /// <summary>
  /// Gets whether this document exists on the server.
  /// </summary>
  public bool IsServer => Source is DocumentSource.Server or DocumentSource.Both;

  /// <summary>
  /// Gets a display label for additional context. Shows "(also local)" for documents in both locations.
  /// </summary>
  public string DisplayLabel => Source == DocumentSource.Both ? "(also local)" : string.Empty;
}
