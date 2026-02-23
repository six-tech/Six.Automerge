// Copyright (c) Six. All rights reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DemoApp.Shared.Controls;

/// <summary>
/// ViewModel for the <see cref="ConnectionStatusBar"/> control.
/// </summary>
[PublicAPI]
public sealed class ConnectionStatusBarViewModel : INotifyPropertyChanged
{
  ConnectionState _connectionState;
  string? _peerId;
  int _peerCount;
  string? _documentId;
  string? _serverUrl;

  /// <inheritdoc />
  public event PropertyChangedEventHandler? PropertyChanged;

  /// <summary>
  /// Gets or sets the current connection state.
  /// </summary>
  public ConnectionState ConnectionState
  {
    get => _connectionState;
    set
    {
      if(_connectionState == value)
      {
        return;
      }

      _connectionState = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ConnectionStateText));
    }
  }

  /// <summary>
  /// Gets or sets the peer ID.
  /// </summary>
  public string? PeerId
  {
    get => _peerId;
    set
    {
      if(_peerId == value)
      {
        return;
      }

      _peerId = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(TruncatedPeerId));
    }
  }

  /// <summary>
  /// Gets or sets the number of connected peers.
  /// </summary>
  public int PeerCount
  {
    get => _peerCount;
    set
    {
      if(_peerCount == value)
      {
        return;
      }

      _peerCount = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  /// Gets or sets the current document ID.
  /// </summary>
  public string? DocumentId
  {
    get => _documentId;
    set
    {
      if(_documentId == value)
      {
        return;
      }

      _documentId = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  /// Gets or sets the server URL.
  /// </summary>
  public string? ServerUrl
  {
    get => _serverUrl;
    set
    {
      if(_serverUrl == value)
      {
        return;
      }

      _serverUrl = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  /// Gets a truncated version of the peer ID for display.
  /// </summary>
  public string TruncatedPeerId
  {
    get
    {
      if(string.IsNullOrEmpty(_peerId))
      {
        return "-";
      }

      return _peerId.Length > 12 ? $"{_peerId[..12]}..." : _peerId;
    }
  }

  /// <summary>
  /// Gets a human-readable text for the connection state.
  /// </summary>
  public string ConnectionStateText => _connectionState switch
  {
    ConnectionState.Disconnected => "Disconnected",
    ConnectionState.Connecting => "Connecting...",
    ConnectionState.Connected => "Connected",
    ConnectionState.Reconnecting => "Reconnecting...",
    _ => "Unknown"
  };

  void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
