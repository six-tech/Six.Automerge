// Copyright (c) Six. All rights reserved.

using DemoApp.Shared;
using DemoApp.Shared.Controls;
using FluentAssertions;
using Xunit;

namespace DemoApp.Shared.Tests;

[Trait("Category", "Unit")]
public class DocumentSessionDialogViewModelTests
{
  [Fact]
  public void DefaultState_HasExpectedDefaults()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.JoinDocumentId.Should().BeEmpty();
    vm.StoredDocuments.Should().BeEmpty();
    vm.Result.Should().BeNull();
  }

  [Fact]
  public void JoinDocumentId_WhenSet_RaisesPropertyChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(DocumentSessionDialogViewModel.JoinDocumentId))
      {
        raised = true;
      }
    };

    vm.JoinDocumentId = "doc-abc123";

    raised.Should().BeTrue();
    vm.JoinDocumentId.Should().Be("doc-abc123");
  }

  [Fact]
  public void CanJoin_WithEmptyDocumentId_IsFalse()
  {
    var vm = new DocumentSessionDialogViewModel { JoinDocumentId = "" };

    vm.CanJoin.Should().BeFalse();
  }

  [Fact]
  public void CanJoin_WithDocumentId_IsTrue()
  {
    var vm = new DocumentSessionDialogViewModel { JoinDocumentId = "doc-abc123" };

    vm.CanJoin.Should().BeTrue();
  }

  [Fact]
  public void CanJoin_WithWhitespaceDocumentId_IsFalse()
  {
    var vm = new DocumentSessionDialogViewModel { JoinDocumentId = "   " };

    vm.CanJoin.Should().BeFalse();
  }

  [Fact]
  public void CreateNew_SetsResultToCreateAction()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.CreateNew();

    vm.Result.Should().NotBeNull();
    vm.Result!.Action.Should().Be(SessionAction.Create);
    vm.Result.DocumentId.Should().BeNull();
  }

  [Fact]
  public void Join_SetsResultToJoinAction()
  {
    var vm = new DocumentSessionDialogViewModel { JoinDocumentId = "doc-abc123" };

    vm.Join();

    vm.Result.Should().NotBeNull();
    vm.Result!.Action.Should().Be(SessionAction.Join);
    vm.Result.DocumentId.Should().Be("doc-abc123");
  }

  [Fact]
  public void Join_WithEmptyId_DoesNotSetResult()
  {
    var vm = new DocumentSessionDialogViewModel { JoinDocumentId = "" };

    vm.Join();

    vm.Result.Should().BeNull();
  }

  [Fact]
  public void Resume_SetsResultToResumeAction()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.Resume("doc-existing");

    vm.Result.Should().NotBeNull();
    vm.Result!.Action.Should().Be(SessionAction.Resume);
    vm.Result.DocumentId.Should().Be("doc-existing");
  }

  [Fact]
  public void LoadStoredDocuments_WithValidDirectory_FindsDocuments()
  {
    // Create temp directory with fake document storage structure
    string tempDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      // FileSystemStorageAdapter uses sharded dir structure:
      // first 2 chars of doc ID as directory, remaining as subdirectory
      // Create two fake document dirs
      Directory.CreateDirectory(Path.Combine(tempDir, "ab", "c123"));
      Directory.CreateDirectory(Path.Combine(tempDir, "de", "f456"));

      var vm = new DocumentSessionDialogViewModel();
      vm.LoadStoredDocuments(tempDir);

      vm.StoredDocuments.Should().HaveCount(2);
      vm.StoredDocuments.Should().Contain("abc123");
      vm.StoredDocuments.Should().Contain("def456");
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public void LoadStoredDocuments_WithEmptyDirectory_ReturnsEmpty()
  {
    string tempDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      var vm = new DocumentSessionDialogViewModel();
      vm.LoadStoredDocuments(tempDir);

      vm.StoredDocuments.Should().BeEmpty();
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public void LoadStoredDocuments_WithNonExistentDirectory_ReturnsEmpty()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.LoadStoredDocuments("/nonexistent/path/12345");

    vm.StoredDocuments.Should().BeEmpty();
  }

  [Fact]
  public void CreateNew_RaisesResultReady()
  {
    var vm = new DocumentSessionDialogViewModel();
    bool raised = false;
    vm.ResultReady += (_, _) => raised = true;

    vm.CreateNew();

    raised.Should().BeTrue();
  }

  [Fact]
  public void Join_WithValidId_RaisesResultReady()
  {
    var vm = new DocumentSessionDialogViewModel { JoinDocumentId = "doc-abc123" };
    bool raised = false;
    vm.ResultReady += (_, _) => raised = true;

    vm.Join();

    raised.Should().BeTrue();
  }

  [Fact]
  public void Resume_RaisesResultReady()
  {
    var vm = new DocumentSessionDialogViewModel();
    bool raised = false;
    vm.ResultReady += (_, _) => raised = true;

    vm.Resume("doc-abc");

    raised.Should().BeTrue();
  }

  [Fact]
  public void JoinDocumentId_WhenChanged_RaisesCanJoinChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.JoinDocumentId = "doc-abc";

    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.CanJoin));
  }

  [Fact]
  public void IsLoading_DefaultsToFalse()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.IsLoading.Should().BeFalse();
  }

  [Fact]
  public void IsLoading_WhenSet_RaisesPropertyChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.IsLoading = true;

    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.IsLoading));
    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.CanJoin));
    vm.IsLoading.Should().BeTrue();
  }

  [Fact]
  public void IsLoading_WhenSetToSameValue_DoesNotRaisePropertyChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, _) => raised = true;

    vm.IsLoading = false; // Same as default

    raised.Should().BeFalse();
  }

  [Fact]
  public void CanJoin_WhenLoading_IsFalse()
  {
    var vm = new DocumentSessionDialogViewModel
    {
      JoinDocumentId = "doc-abc",
      IsLoading = true
    };

    vm.CanJoin.Should().BeFalse();
  }

  [Fact]
  public void ErrorMessage_DefaultsToNull()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.ErrorMessage.Should().BeNull();
    vm.HasError.Should().BeFalse();
  }

  [Fact]
  public void ErrorMessage_WhenSet_RaisesPropertyChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.ErrorMessage = "Connection failed";

    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.ErrorMessage));
    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.HasError));
    vm.ErrorMessage.Should().Be("Connection failed");
    vm.HasError.Should().BeTrue();
  }

  [Fact]
  public void ErrorMessage_WhenCleared_HasErrorIsFalse()
  {
    var vm = new DocumentSessionDialogViewModel { ErrorMessage = "Some error" };

    vm.ErrorMessage = null;

    vm.HasError.Should().BeFalse();
  }

  [Fact]
  public void ErrorMessage_WhenSetToSameValue_DoesNotRaisePropertyChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    bool raised = false;
    vm.PropertyChanged += (_, _) => raised = true;

    vm.ErrorMessage = null; // Same as default

    raised.Should().BeFalse();
  }

  [Fact]
  public void DefaultState_HasExpectedLoadingAndErrorDefaults()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.IsLoading.Should().BeFalse();
    vm.ErrorMessage.Should().BeNull();
    vm.HasError.Should().BeFalse();
  }

  // --- ServerDocuments and AllDocuments tests ---

  [Fact]
  public void ServerDocuments_DefaultsToEmpty()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.ServerDocuments.Should().BeEmpty();
  }

  [Fact]
  public void AllDocuments_DefaultsToEmpty()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.AllDocuments.Should().BeEmpty();
  }

  [Fact]
  public void AllDocuments_WithOnlyLocalDocuments_ShowsLocalSource()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.StoredDocuments.Add("doc-abc123");

    vm.RefreshAllDocuments();

    vm.AllDocuments.Should().HaveCount(1);
    vm.AllDocuments[0].DocumentId.Should().Be("doc-abc123");
    vm.AllDocuments[0].Source.Should().Be(DocumentSource.Local);
  }

  [Fact]
  public void AllDocuments_WithOnlyServerDocuments_ShowsServerSource()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.ServerDocuments.Add("doc-server1");

    vm.RefreshAllDocuments();

    vm.AllDocuments.Should().HaveCount(1);
    vm.AllDocuments[0].DocumentId.Should().Be("doc-server1");
    vm.AllDocuments[0].Source.Should().Be(DocumentSource.Server);
  }

  [Fact]
  public void AllDocuments_WithOverlappingDocuments_ShowsBothSource()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.StoredDocuments.Add("doc-shared");
    vm.ServerDocuments.Add("doc-shared");

    vm.RefreshAllDocuments();

    vm.AllDocuments.Should().HaveCount(1);
    vm.AllDocuments[0].DocumentId.Should().Be("doc-shared");
    vm.AllDocuments[0].Source.Should().Be(DocumentSource.Both);
  }

  [Fact]
  public void AllDocuments_WithMixedDocuments_MergesCorrectly()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.StoredDocuments.Add("doc-local-only");
    vm.StoredDocuments.Add("doc-shared");
    vm.ServerDocuments.Add("doc-shared");
    vm.ServerDocuments.Add("doc-server-only");

    vm.RefreshAllDocuments();

    vm.AllDocuments.Should().HaveCount(3);

    DocumentEntry localOnly = vm.AllDocuments.First(d => d.DocumentId == "doc-local-only");
    localOnly.Source.Should().Be(DocumentSource.Local);

    DocumentEntry shared = vm.AllDocuments.First(d => d.DocumentId == "doc-shared");
    shared.Source.Should().Be(DocumentSource.Both);

    DocumentEntry serverOnly = vm.AllDocuments.First(d => d.DocumentId == "doc-server-only");
    serverOnly.Source.Should().Be(DocumentSource.Server);
  }

  [Fact]
  public void AllDocuments_RaisesPropertyChanged_WhenRefreshed()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.StoredDocuments.Add("doc-abc");
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.RefreshAllDocuments();

    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.AllDocuments));
  }

  [Fact]
  public void LoadStoredDocuments_AlsoRefreshesAllDocuments()
  {
    string tempDir = Path.Combine(Path.GetTempPath(), $"rayx-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      Directory.CreateDirectory(Path.Combine(tempDir, "ab", "c123"));

      var vm = new DocumentSessionDialogViewModel();
      vm.ServerDocuments.Add("doc-server1");
      vm.LoadStoredDocuments(tempDir);

      // AllDocuments should contain both the local and server docs
      vm.AllDocuments.Should().HaveCount(2);
      vm.AllDocuments.Should().Contain(d => d.DocumentId == "abc123" && d.Source == DocumentSource.Local);
      vm.AllDocuments.Should().Contain(d => d.DocumentId == "doc-server1" && d.Source == DocumentSource.Server);
    }
    finally
    {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public void DocumentEntry_DisplayLabel_ForLocalOnly()
  {
    var entry = new DocumentEntry("doc-abc", DocumentSource.Local);

    entry.DisplayLabel.Should().BeEmpty();
  }

  [Fact]
  public void DocumentEntry_DisplayLabel_ForServerOnly()
  {
    var entry = new DocumentEntry("doc-abc", DocumentSource.Server);

    entry.DisplayLabel.Should().BeEmpty();
  }

  [Fact]
  public void DocumentEntry_DisplayLabel_ForBoth()
  {
    var entry = new DocumentEntry("doc-abc", DocumentSource.Both);

    entry.DisplayLabel.Should().Be("(also local)");
  }

  [Fact]
  public void DocumentEntry_IsLocal_ReturnsCorrectly()
  {
    new DocumentEntry("a", DocumentSource.Local).IsLocal.Should().BeTrue();
    new DocumentEntry("a", DocumentSource.Server).IsLocal.Should().BeFalse();
    new DocumentEntry("a", DocumentSource.Both).IsLocal.Should().BeTrue();
  }

  [Fact]
  public void DocumentEntry_IsServer_ReturnsCorrectly()
  {
    new DocumentEntry("a", DocumentSource.Local).IsServer.Should().BeFalse();
    new DocumentEntry("a", DocumentSource.Server).IsServer.Should().BeTrue();
    new DocumentEntry("a", DocumentSource.Both).IsServer.Should().BeTrue();
  }

  [Fact]
  public void IsLoadingServerDocuments_DefaultsToFalse()
  {
    var vm = new DocumentSessionDialogViewModel();

    vm.IsLoadingServerDocuments.Should().BeFalse();
  }

  [Fact]
  public void IsLoadingServerDocuments_WhenSet_RaisesPropertyChanged()
  {
    var vm = new DocumentSessionDialogViewModel();
    List<string> changedProps = [];
    vm.PropertyChanged += (_, args) => changedProps.Add(args.PropertyName!);

    vm.IsLoadingServerDocuments = true;

    changedProps.Should().Contain(nameof(DocumentSessionDialogViewModel.IsLoadingServerDocuments));
    vm.IsLoadingServerDocuments.Should().BeTrue();
  }

  [Fact]
  public void Resume_WithDocumentEntry_SetsResultToResumeAction()
  {
    var vm = new DocumentSessionDialogViewModel();
    var entry = new DocumentEntry("doc-abc123", DocumentSource.Server);

    vm.Resume(entry.DocumentId);

    vm.Result.Should().NotBeNull();
    vm.Result!.Action.Should().Be(SessionAction.Resume);
    vm.Result.DocumentId.Should().Be("doc-abc123");
  }

  // --- SubscribeToDocumentsChanged tests ---

  [Fact]
  public void SubscribeToDocumentsChanged_WithNullRepoService_ThrowsArgumentNullException()
  {
    var vm = new DocumentSessionDialogViewModel();

    Action act = () => vm.SubscribeToDocumentsChanged(null!);

    act.Should().Throw<ArgumentNullException>();
  }

  [Fact]
  public void SubscribeToDocumentsChanged_StoresRepoServiceReference()
  {
    var vm = new DocumentSessionDialogViewModel();
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/test",
      AppName = "TestApp"
    };
    var service = new RepoService(options);

    // Should not throw
    vm.SubscribeToDocumentsChanged(service);

    // Unsubscribe should also not throw
    vm.UnsubscribeFromDocumentsChanged();

    service.Dispose();
  }

  [Fact]
  public void UnsubscribeFromDocumentsChanged_WhenNotSubscribed_DoesNotThrow()
  {
    var vm = new DocumentSessionDialogViewModel();

    Action act = () => vm.UnsubscribeFromDocumentsChanged();

    act.Should().NotThrow();
  }

  // --- LoadServerDocumentsAsync tests ---

  [Fact]
  public async Task LoadServerDocumentsAsync_WithNullRepoService_ShowsOnlyLocalDocuments()
  {
    var vm = new DocumentSessionDialogViewModel();
    vm.StoredDocuments.Add("doc-local");

    await vm.LoadServerDocumentsAsync(null);

    vm.ServerDocuments.Should().BeEmpty();
    vm.AllDocuments.Should().HaveCount(1);
    vm.AllDocuments[0].Source.Should().Be(DocumentSource.Local);
  }

  [Fact]
  public async Task LoadServerDocumentsAsync_WithDisconnectedService_ShowsOnlyLocalDocuments()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/test",
      AppName = "TestApp"
    };
    var service = new RepoService(options);

    var vm = new DocumentSessionDialogViewModel();
    vm.StoredDocuments.Add("doc-local");

    // Service not initialized = not connected
    await vm.LoadServerDocumentsAsync(service);

    vm.ServerDocuments.Should().BeEmpty();
    vm.AllDocuments.Should().HaveCount(1);
    vm.AllDocuments[0].Source.Should().Be(DocumentSource.Local);

    service.Dispose();
  }

  [Fact]
  public async Task LoadServerDocumentsAsync_SetsIsLoadingServerDocuments()
  {
    var vm = new DocumentSessionDialogViewModel();
    List<bool> loadingStates = [];
    vm.PropertyChanged += (_, args) =>
    {
      if(args.PropertyName == nameof(DocumentSessionDialogViewModel.IsLoadingServerDocuments))
      {
        loadingStates.Add(vm.IsLoadingServerDocuments);
      }
    };

    await vm.LoadServerDocumentsAsync(null);

    // Should have been set to true then false
    loadingStates.Should().Equal([true, false]);
  }
}
