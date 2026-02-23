// Copyright (c) Six. All rights reserved.

using DemoApp.Shared.Controls;
using FluentAssertions;
using Xunit;

namespace DemoApp.Shared.Tests;

[Trait("Category", "Unit")]
public class ConnectionStatusBarViewModelTests
{
  [Fact]
  public void DefaultState_HasExpectedDefaults()
  {
    var vm = new ConnectionStatusBarViewModel();

    vm.ConnectionState.Should().Be(ConnectionState.Disconnected);
    vm.PeerId.Should().BeNull();
    vm.PeerCount.Should().Be(0);
    vm.DocumentId.Should().BeNull();
    vm.ServerUrl.Should().BeNull();
  }

  [Fact]
  public void ConnectionState_WhenSet_RaisesPropertyChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(ConnectionStatusBarViewModel.ConnectionState))
      {
        raised = true;
      }
    };

    vm.ConnectionState = ConnectionState.Connected;

    raised.Should().BeTrue();
    vm.ConnectionState.Should().Be(ConnectionState.Connected);
  }

  [Fact]
  public void PeerId_WhenSet_RaisesPropertyChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(ConnectionStatusBarViewModel.PeerId))
      {
        raised = true;
      }
    };

    vm.PeerId = "peer-abc12345";

    raised.Should().BeTrue();
    vm.PeerId.Should().Be("peer-abc12345");
  }

  [Fact]
  public void PeerCount_WhenSet_RaisesPropertyChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(ConnectionStatusBarViewModel.PeerCount))
      {
        raised = true;
      }
    };

    vm.PeerCount = 3;

    raised.Should().BeTrue();
    vm.PeerCount.Should().Be(3);
  }

  [Fact]
  public void DocumentId_WhenSet_RaisesPropertyChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(ConnectionStatusBarViewModel.DocumentId))
      {
        raised = true;
      }
    };

    vm.DocumentId = "doc-xyz";

    raised.Should().BeTrue();
    vm.DocumentId.Should().Be("doc-xyz");
  }

  [Fact]
  public void ServerUrl_WhenSet_RaisesPropertyChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(ConnectionStatusBarViewModel.ServerUrl))
      {
        raised = true;
      }
    };

    vm.ServerUrl = "https://localhost:5001";

    raised.Should().BeTrue();
    vm.ServerUrl.Should().Be("https://localhost:5001");
  }

  [Fact]
  public void TruncatedPeerId_WithLongPeerId_TruncatesTo12Chars()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      PeerId = "peer-abcdefghijklmnop"
    };

    vm.TruncatedPeerId.Should().Be("peer-abcdefg...");
  }

  [Fact]
  public void TruncatedPeerId_WithShortPeerId_ReturnsFullId()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      PeerId = "peer-abc"
    };

    vm.TruncatedPeerId.Should().Be("peer-abc");
  }

  [Fact]
  public void TruncatedPeerId_WithNullPeerId_ReturnsEmptyDash()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      PeerId = null
    };

    vm.TruncatedPeerId.Should().Be("-");
  }

  [Fact]
  public void ConnectionStateText_Disconnected_ReturnsExpectedText()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      ConnectionState = ConnectionState.Disconnected
    };

    vm.ConnectionStateText.Should().Be("Disconnected");
  }

  [Fact]
  public void ConnectionStateText_Connected_ReturnsExpectedText()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      ConnectionState = ConnectionState.Connected
    };

    vm.ConnectionStateText.Should().Be("Connected");
  }

  [Fact]
  public void ConnectionStateText_Connecting_ReturnsExpectedText()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      ConnectionState = ConnectionState.Connecting
    };

    vm.ConnectionStateText.Should().Be("Connecting...");
  }

  [Fact]
  public void ConnectionStateText_Reconnecting_ReturnsExpectedText()
  {
    var vm = new ConnectionStatusBarViewModel
    {
      ConnectionState = ConnectionState.Reconnecting
    };

    vm.ConnectionStateText.Should().Be("Reconnecting...");
  }

  [Fact]
  public void PeerId_WhenChanged_AlsoRaisesTruncatedPeerIdChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.PeerId = "peer-abcdefghijklmnop";

    changedProps.Should().Contain(nameof(ConnectionStatusBarViewModel.TruncatedPeerId));
  }

  [Fact]
  public void ConnectionState_WhenChanged_AlsoRaisesConnectionStateTextChanged()
  {
    var vm = new ConnectionStatusBarViewModel();
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.ConnectionState = ConnectionState.Connected;

    changedProps.Should().Contain(nameof(ConnectionStatusBarViewModel.ConnectionStateText));
  }
}
