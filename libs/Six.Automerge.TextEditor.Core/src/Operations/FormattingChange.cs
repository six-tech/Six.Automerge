// Copyright (c) Six. Licensed under the MIT License.

namespace Six.Automerge.TextEdit;

/// <summary>
/// Payload for <see cref="TextDocument.FormattedTextChanged"/> observable notifications.
/// </summary>
[PublicAPI]
public readonly record struct FormattingChange(FormattedText FormattedText, bool IsLocal);
