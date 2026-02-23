// Copyright (c) Six. All rights reserved.

using DemoApp.CollaborativeEditor.ViewModels;
using FluentAssertions;
using Six.Automerge;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.TextEdit;
using Xunit;

namespace DemoApp.CollaborativeEditor.Tests;

[Trait("Category", "Unit")]
public sealed class EditorViewModelTests : IDisposable
{
  readonly EditorViewModel _viewModel = new(dispatcher: action => action());

  [Fact]
  public void DocumentText_OnNew_ShouldBeEmpty()
  {
    _viewModel.DocumentText.Should().BeEmpty();
  }

  [Fact]
  public async Task DocumentText_WhenSet_WithTextDocument_ShouldApplyChange()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);
    _viewModel.DocumentText = "Hello World";

    _viewModel.DocumentText.Should().Be("Hello World");
  }

  [Fact]
  public async Task DocumentText_WhenEdited_ShouldComputeDiffAndSplice()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);
    _viewModel.DocumentText = "Hello World";
    _viewModel.DocumentText = "Hello Automerge";

    _viewModel.DocumentText.Should().Be("Hello Automerge");
  }

  [Fact]
  public void DocumentText_WhenSetWithoutService_ShouldNotThrow()
  {
    _viewModel.DocumentText = "No service";
    _viewModel.DocumentText.Should().Be("No service");
  }

  [Fact]
  public void SyncStatus_OnNew_ShouldBeDisconnected()
  {
    _viewModel.SyncStatus.Should().Be("Disconnected");
  }

  [Fact]
  public void CursorPosition_DefaultIsZero()
  {
    _viewModel.CursorPosition.Should().Be(0);
  }

  [Fact]
  public void SelectionLength_DefaultIsZero()
  {
    _viewModel.SelectionLength.Should().Be(0);
  }

  [Fact]
  public void IsBold_OnNew_ShouldBeFalse()
  {
    _viewModel.IsBold.Should().BeFalse();
  }

  [Fact]
  public void IsItalic_OnNew_ShouldBeFalse()
  {
    _viewModel.IsItalic.Should().BeFalse();
  }

  [Fact]
  public void IsUnderline_OnNew_ShouldBeFalse()
  {
    _viewModel.IsUnderline.Should().BeFalse();
  }

  [Fact]
  public void SaveCommand_ShouldNotBeNull()
  {
    _viewModel.SaveCommand.Should().NotBeNull();
  }

  [Fact]
  public void ExportJsonCommand_ShouldNotBeNull()
  {
    _viewModel.ExportJsonCommand.Should().NotBeNull();
  }

  [Fact]
  public void ToggleBoldCommand_ShouldNotBeNull()
  {
    _viewModel.ToggleBoldCommand.Should().NotBeNull();
  }

  [Fact]
  public void ToggleItalicCommand_ShouldNotBeNull()
  {
    _viewModel.ToggleItalicCommand.Should().NotBeNull();
  }

  [Fact]
  public void ToggleUnderlineCommand_ShouldNotBeNull()
  {
    _viewModel.ToggleUnderlineCommand.Should().NotBeNull();
  }

  [Fact]
  public void Dispose_ShouldPreventExceptions()
  {
    _viewModel.Dispose();

    // Should not throw when disposed again
    _viewModel.Dispose();
  }

  [Fact]
  public async Task DocumentText_SetSameValue_ShouldNotRaisePropertyChanged()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);
    _viewModel.DocumentText = "Test";

    bool raised = false;
    _viewModel.PropertyChanged += (_, e) =>
    {
      if(e.PropertyName == nameof(EditorViewModel.DocumentText))
      {
        raised = true;
      }
    };

    _viewModel.DocumentText = "Test";

    raised.Should().BeFalse();
  }

  [Fact]
  public async Task SetTextDocument_ShouldRefreshDocumentText()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();

    // Pre-populate text in the document before setting it on ViewModel
    await textDoc.ApplyTextChangeAsync("Preloaded text");
    // Wait for debouncer to flush
    await Task.Delay(200);

    _viewModel.SetTextDocument(textDoc);

    _viewModel.DocumentText.Should().Be("Preloaded text");
  }

  [Fact]
  public void TextDocument_BeforeSet_ShouldBeNull()
  {
    _viewModel.TextDocument.Should().BeNull();
  }

  [Fact]
  public async Task TextDocument_AfterSet_ShouldNotBeNull()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);

    _viewModel.TextDocument.Should().NotBeNull();
  }

  [Fact]
  public void RepoService_WhenNotProvided_ShouldBeNull()
  {
    _viewModel.RepoService.Should().BeNull();
  }

  [Fact]
  public async Task RemoteChange_ShouldUpdateDocumentText()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    // Set initial text
    vm.DocumentText = "Hello";
    // Wait for debouncer to flush the local change
    await Task.Delay(200);

    // Simulate a remote change by forking, editing, merging, and notifying
    var textId = await textDoc.Handle.ReadAsync(
      doc => doc.GetObject(doc.RootId, "content"));
    using AutomergeDocument forkedDoc = await textDoc.Handle.ReadAsync(doc => doc.Fork());
    forkedDoc.SpliceText(textId!, 5, 0, " World");
    forkedDoc.Commit("remote edit");

    var underlyingDoc = ((IDocHandle)textDoc.Handle).GetDocument()!;
    underlyingDoc.Merge(forkedDoc);
    await textDoc.Handle.NotifyRemoteChangeAsync(underlyingDoc);

    // Allow remote refresh throttle (100ms) + async processing
    await Task.Delay(300);

    vm.DocumentText.Should().Be("Hello World");
  }

  [Fact]
  public async Task FormattingState_AfterBoldMark_ShouldUpdateIsBold()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    // Wait for debouncer to flush
    await Task.Delay(200);

    // Select range and mark bold
    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleBoldCommand.Execute().Subscribe();

    // Wait for async mark operation
    await Task.Delay(200);

    // Move cursor into bolded range
    vm.CursorPosition = 2;

    vm.IsBold.Should().BeTrue();
  }

  [Fact]
  public async Task SaveDocument_WithTextDocument_ShouldUpdateSyncStatus()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);
    _viewModel.DocumentText = "Hello";
    await Task.Delay(200);

    _viewModel.SaveCommand.Execute().Subscribe();
    await Task.Delay(200);

    _viewModel.SyncStatus.Should().StartWith("Saved (");
  }

  [Fact]
  public void SaveDocument_WithoutTextDocument_ShouldShowNoDocumentLoaded()
  {
    _viewModel.SaveCommand.Execute().Subscribe();

    _viewModel.SyncStatus.Should().Be("No document loaded");
  }

  [Fact]
  public void ExportJson_WithoutTextDocument_ShouldShowNoDocumentLoaded()
  {
    _viewModel.ExportJsonCommand.Execute().Subscribe();

    _viewModel.SyncStatus.Should().Be("No document loaded");
  }

  [Fact]
  public async Task ApplyTextChange_WithTextDocument_ShouldNotDeadlock()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    // This exercises ApplyTextChangeAsync through the DocumentText setter.
    // If lock handling is wrong, this deadlocks.
    vm.DocumentText = "First edit";
    vm.DocumentText = "Second edit";
    vm.DocumentText = "Third edit with more text";

    vm.DocumentText.Should().Be("Third edit with more text");

    // Wait for debounced changes to flush
    await Task.Delay(200);

    textDoc.Text.Should().Be("Third edit with more text");
  }

  [Fact]
  public async Task RapidCharacterInserts_ShouldNotBlockAndShouldConverge()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    // Simulate typing character by character
    string text = "The quick brown fox jumps over the lazy dog.";
    var sb = new System.Text.StringBuilder();
    for(int i = 0; i < text.Length; i++)
    {
      sb.Append(text[i]);
      vm.DocumentText = sb.ToString();
    }

    vm.DocumentText.Should().Be(text);

    // Wait for all debounced changes to flush
    await Task.Delay(500);

    textDoc.Text.Should().Be(text);
  }

  [Fact]
  public async Task RapidTypingWithNewlinesAndBackspaces_ShouldConverge()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    // Type "Hello\nWorld" character by character
    vm.DocumentText = "H";
    vm.DocumentText = "He";
    vm.DocumentText = "Hel";
    vm.DocumentText = "Hell";
    vm.DocumentText = "Hello";
    vm.DocumentText = "Hello\n";
    vm.DocumentText = "Hello\nW";
    vm.DocumentText = "Hello\nWo";
    vm.DocumentText = "Hello\nWor";
    vm.DocumentText = "Hello\nWorl";
    vm.DocumentText = "Hello\nWorld";

    // Simulate backspace (delete last char)
    vm.DocumentText = "Hello\nWorl";
    vm.DocumentText = "Hello\nWorld!";

    vm.DocumentText.Should().Be("Hello\nWorld!");

    await Task.Delay(500);

    textDoc.Text.Should().Be("Hello\nWorld!");
  }

  [Fact]
  public void UpdateConnectionStatus_Connected_WithPeers_ShouldShowSyncingWithPeerCount()
  {
    _viewModel.UpdateConnectionStatus(DemoApp.Shared.ConnectionState.Connected, 3);

    _viewModel.SyncStatus.Should().Be("Syncing (3 peers)");
  }

  [Fact]
  public void UpdateConnectionStatus_Connected_OnePeer_ShouldShowSyncingOnePeer()
  {
    _viewModel.UpdateConnectionStatus(DemoApp.Shared.ConnectionState.Connected, 1);

    _viewModel.SyncStatus.Should().Be("Syncing (1 peer)");
  }

  [Fact]
  public void UpdateConnectionStatus_Connected_NoPeers_ShouldShowReady()
  {
    _viewModel.UpdateConnectionStatus(DemoApp.Shared.ConnectionState.Connected, 0);

    _viewModel.SyncStatus.Should().Be("Ready");
  }

  [Fact]
  public void UpdateConnectionStatus_Disconnected_ShouldShowDisconnected()
  {
    _viewModel.UpdateConnectionStatus(DemoApp.Shared.ConnectionState.Disconnected, 0);

    _viewModel.SyncStatus.Should().Be("Disconnected");
  }

  [Fact]
  public void UpdateConnectionStatus_Reconnecting_ShouldShowReconnecting()
  {
    _viewModel.UpdateConnectionStatus(DemoApp.Shared.ConnectionState.Reconnecting, 2);

    _viewModel.SyncStatus.Should().Be("Reconnecting...");
  }

  [Fact]
  public void UpdateConnectionStatus_Connecting_ShouldShowConnecting()
  {
    _viewModel.UpdateConnectionStatus(DemoApp.Shared.ConnectionState.Connecting, 0);

    _viewModel.SyncStatus.Should().Be("Connecting...");
  }

  [Fact]
  public void FormattedText_OnNew_ShouldBeNull()
  {
    _viewModel.FormattedText.Should().BeNull();
  }

  [Fact]
  public async Task FormattedText_AfterSetTextDocument_ShouldNotBeNull()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);

    _viewModel.FormattedText.Should().NotBeNull();
  }

  [Fact]
  public async Task FormattedText_AfterBoldMark_ShouldContainBoldSpan()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleBoldCommand.Execute().Subscribe();
    await Task.Delay(200);

    // FormattedText should have spans with bold mark
    vm.FormattedText.Should().NotBeNull();
    vm.FormattedText!.Spans.Should().HaveCountGreaterThan(0);
    vm.FormattedText.Spans[0].Marks.Should().ContainKey("bold");
  }

  [Fact]
  public async Task FormattingState_AfterItalicMark_ShouldUpdateIsItalic()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleItalicCommand.Execute().Subscribe();
    await Task.Delay(200);

    vm.CursorPosition = 2;

    vm.IsItalic.Should().BeTrue();
    vm.IsBold.Should().BeFalse();
  }

  [Fact]
  public async Task FormattingState_AfterUnderlineMark_ShouldUpdateIsUnderline()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleUnderlineCommand.Execute().Subscribe();
    await Task.Delay(200);

    vm.CursorPosition = 2;

    vm.IsUnderline.Should().BeTrue();
    vm.IsBold.Should().BeFalse();
  }

  [Fact]
  public async Task ToggleBold_WhenAlreadyBold_ShouldRemoveBold()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    // Apply bold
    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleBoldCommand.Execute().Subscribe();
    await Task.Delay(200);

    // Verify bold is on
    vm.CursorPosition = 2;
    vm.IsBold.Should().BeTrue();

    // Remove bold (toggle off)
    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleBoldCommand.Execute().Subscribe();
    await Task.Delay(200);

    vm.CursorPosition = 2;
    vm.IsBold.Should().BeFalse();
  }

  [Fact]
  public async Task ExportJson_WithTextDocument_ShouldUpdateSyncStatus()
  {
    using TextDocument textDoc = await CreateTestDocumentAsync();
    _viewModel.SetTextDocument(textDoc);
    _viewModel.DocumentText = "Hello";
    await Task.Delay(200);

    _viewModel.ExportJsonCommand.Execute().Subscribe();
    await Task.Delay(200);

    _viewModel.SyncStatus.Should().StartWith("Exported JSON (");
  }

  [Fact]
  public async Task CursorPosition_OutsideFormattedRange_ShouldResetFormatting()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    // Apply bold to "Hello" (0-5)
    vm.CursorPosition = 0;
    vm.SelectionLength = 5;
    vm.ToggleBoldCommand.Execute().Subscribe();
    await Task.Delay(200);

    // Move cursor into bold range
    vm.CursorPosition = 2;
    vm.IsBold.Should().BeTrue();

    // Move cursor outside bold range
    vm.CursorPosition = 8;
    vm.IsBold.Should().BeFalse();
  }

  [Fact]
  public async Task FormattedText_AfterTextChange_ShouldUpdate()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello";
    await Task.Delay(200);

    vm.FormattedText.Should().NotBeNull();
    vm.FormattedText!.PlainText.Should().Be("Hello");

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    vm.FormattedText!.PlainText.Should().Be("Hello World");
  }

  [Fact]
  public async Task ToggleBold_WithNoSelection_ShouldNotApplyMark()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    // No selection (SelectionLength = 0)
    vm.CursorPosition = 0;
    vm.SelectionLength = 0;
    vm.ToggleBoldCommand.Execute().Subscribe();
    await Task.Delay(200);

    // Should not crash or apply mark
    vm.FormattedText!.Spans.Should().AllSatisfy(span =>
      span.Marks.Should().NotContainKey("bold"));
  }

  [Fact]
  public async Task FormattedText_AfterItalicMark_ShouldContainItalicSpan()
  {
    using var vm = new EditorViewModel(dispatcher: action => action());
    using TextDocument textDoc = await CreateTestDocumentAsync();
    vm.SetTextDocument(textDoc);

    vm.DocumentText = "Hello World";
    await Task.Delay(200);

    vm.CursorPosition = 6;
    vm.SelectionLength = 5;
    vm.ToggleItalicCommand.Execute().Subscribe();
    await Task.Delay(200);

    vm.FormattedText.Should().NotBeNull();
    vm.FormattedText!.Spans.Should().Contain(span => span.Marks.ContainsKey("italic"));
  }

  static async Task<TextDocument> CreateTestDocumentAsync()
  {
    var networkAdapter = new InMemoryNetworkAdapter();
    var config = new RepoConfig { NetworkAdapters = [networkAdapter] };
    var repo = new Repo(config);
    return await TextDocument.CreateAsync(repo);
  }

  public void Dispose()
  {
    _viewModel.Dispose();
  }
}
