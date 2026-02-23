// Copyright (c) Six. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DemoApp.Shared.Controls;

/// <summary>
/// A reusable status bar showing connection state, peer info, document ID, and server URL.
/// </summary>
[PublicAPI]
public partial class ConnectionStatusBar : UserControl
{
  public ConnectionStatusBar()
  {
    InitializeComponent();
  }

  /// <inheritdoc />
  protected override void OnDataContextChanged(EventArgs e)
  {
    base.OnDataContextChanged(e);

    if(DataContext is ConnectionStatusBarViewModel vm)
    {
      vm.PropertyChanged += OnViewModelPropertyChanged;
      UpdateConnectionDot(vm.ConnectionState);
    }
  }

  void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if(e.PropertyName == nameof(ConnectionStatusBarViewModel.ConnectionState)
       && sender is ConnectionStatusBarViewModel vm)
    {
      UpdateConnectionDot(vm.ConnectionState);
    }
  }

  void UpdateConnectionDot(ConnectionState state)
  {
    Ellipse? dot = this.FindControl<Ellipse>("ConnectionDot");
    if(dot is null)
    {
      return;
    }

    dot.Fill = state switch
    {
      ConnectionState.Connected => new SolidColorBrush(Color.Parse("#00CC00")),
      ConnectionState.Connecting => new SolidColorBrush(Color.Parse("#FFAA00")),
      ConnectionState.Reconnecting => new SolidColorBrush(Color.Parse("#FFAA00")),
      _ => new SolidColorBrush(Color.Parse("#CC0000"))
    };
  }

  async void OnCopyDocumentId(object? sender, RoutedEventArgs e)
  {
    if(DataContext is not ConnectionStatusBarViewModel vm || string.IsNullOrEmpty(vm.DocumentId))
    {
      return;
    }

    IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
    if(clipboard is not null)
    {
      await clipboard.SetTextAsync(vm.DocumentId);
    }
  }
}
