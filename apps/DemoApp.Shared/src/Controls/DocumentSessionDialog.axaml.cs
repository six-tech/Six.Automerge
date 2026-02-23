// Copyright (c) Six. All rights reserved.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DemoApp.Shared.Controls;

/// <summary>
/// A startup dialog for creating, joining, or resuming a document session.
/// </summary>
[PublicAPI]
public partial class DocumentSessionDialog : UserControl
{
  public DocumentSessionDialog()
  {
    InitializeComponent();
  }

  void OnCreateNew(object? sender, RoutedEventArgs e)
  {
    if(DataContext is DocumentSessionDialogViewModel vm)
    {
      vm.CreateNew();
    }
  }

  void OnJoin(object? sender, RoutedEventArgs e)
  {
    if(DataContext is DocumentSessionDialogViewModel vm)
    {
      vm.Join();
    }
  }

  void OnResumeDocument(object? sender, RoutedEventArgs e)
  {
    if(sender is Button { Tag: string documentId } && DataContext is DocumentSessionDialogViewModel vm)
    {
      vm.Resume(documentId);
    }
  }

  void OnResumeDocumentEntry(object? sender, RoutedEventArgs e)
  {
    if(sender is Button { Tag: string documentId } && DataContext is DocumentSessionDialogViewModel vm)
    {
      vm.Resume(documentId);
    }
  }

  /// <summary>
  /// Raised when the user clicks the Refresh button to re-fetch server documents.
  /// </summary>
  public event EventHandler? RefreshRequested;

  void OnRefreshServerDocuments(object? sender, RoutedEventArgs e)
  {
    RefreshRequested?.Invoke(this, EventArgs.Empty);
  }
}
