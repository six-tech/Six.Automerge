// Copyright (c) Six. All rights reserved.

using FluentAssertions;
using Six.Automerge;
using Six.Automerge.Repository;
using Six.Automerge.Repository.Network;
using Six.Automerge.TextEdit;
using Xunit;
using Xunit.Abstractions;

namespace DemoApp.CollaborativeEditor.Tests;

/// <summary>
/// Multi-peer integration tests: two Repos connected via InMemoryNetworkAdapter
/// sharing a TextDocument through Automerge sync protocol.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MultiPeerScenarioTests : IDisposable
{
  readonly ITestOutputHelper _output;
  readonly InMemoryNetworkAdapter _adapterA;
  readonly InMemoryNetworkAdapter _adapterB;
  readonly Repo _repoA;
  readonly Repo _repoB;

  public MultiPeerScenarioTests(ITestOutputHelper output)
  {
    _output = output;

    _adapterA = new InMemoryNetworkAdapter();
    _adapterB = new InMemoryNetworkAdapter();

    // Create repos BEFORE connecting adapters so PeerCandidate events are received
    _repoA = new Repo(new RepoConfig { NetworkAdapters = [_adapterA] });
    _repoB = new Repo(new RepoConfig { NetworkAdapters = [_adapterB] });

    _adapterA.ConnectTo(_adapterB);
  }

  public void Dispose()
  {
    _repoA.Dispose();
    _repoB.Dispose();
  }

  [Fact]
  public async Task CreateOnA_FindOnB_ShouldWork()
  {
    // Create a raw document on A
    var handleA = await _repoA.Create<object>();
    _output.WriteLine($"Created doc on A: {handleA.DocumentId}");

    // Try to find it on B
    var handleB = await _repoB.FindAsync<object>(handleA.DocumentId, TimeSpan.FromSeconds(5));

    handleB.Should().NotBeNull("Repo B should find document from Repo A via InMemoryNetworkAdapter");
    _output.WriteLine($"Found doc on B: {handleB!.DocumentId}");
  }

  [Fact]
  public async Task CreateTextDocumentOnA_OpenOnB_ShouldFindDocument()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    _output.WriteLine($"Created TextDocument on A: {docA.DocumentId}");

    // OpenAsync may need retries because the sync protocol first transfers the
    // empty document (marking the handle Ready), then the "text" field change
    // arrives in a subsequent sync round.
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);

    docB.Should().NotBeNull("Repo B should open TextDocument from Repo A");
    docB!.DocumentId.Should().Be(docA.DocumentId);
    _output.WriteLine($"Opened TextDocument on B: {docB.DocumentId}");
  }

  [Fact]
  public async Task TypeOnA_ShouldPropagateToB()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);
    docB.Should().NotBeNull();
    _output.WriteLine("Both documents open, typing on A");

    // Type on A
    await docA.ApplyTextChangeAsync("Hello World");
    await Task.Delay(300); // debounce flush

    // Wait for B to receive the change via sync
    await WaitUntilAsync(() => docB!.Text == "Hello World", TimeSpan.FromSeconds(5));

    docB!.Text.Should().Be("Hello World");
  }

  [Fact]
  public async Task TypeOnB_ShouldPropagateToA()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);
    docB.Should().NotBeNull();

    // Type on B
    await docB!.ApplyTextChangeAsync("From Peer B");
    await Task.Delay(300);

    // Wait for A to receive the change
    await WaitUntilAsync(() => docA.Text == "From Peer B", TimeSpan.FromSeconds(5));

    docA.Text.Should().Be("From Peer B");
  }

  [Fact]
  public async Task ApplyBoldMarkOnA_BShouldReceiveFormattedSpans()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);
    docB.Should().NotBeNull();

    // Write text on A and flush
    await docA.ApplyTextChangeAsync("Hello World");
    await Task.Delay(300);
    await WaitUntilAsync(() => docB!.Text == "Hello World", TimeSpan.FromSeconds(5));

    // Apply bold mark on A ("Hello" chars 0-5)
    await docA.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);
    await Task.Delay(500);

    // Wait for B to receive the formatted change
    await WaitUntilAsync(
      () => docB!.FormattedText.Spans.Any(s => s.Marks.ContainsKey("bold")),
      TimeSpan.FromSeconds(5));

    docB!.FormattedText.Spans.Should().Contain(span => span.Marks.ContainsKey("bold"));
  }

  [Fact]
  public async Task ConcurrentTyping_BothPeers_ShouldConverge()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);
    docB.Should().NotBeNull();

    // A types first
    await docA.ApplyTextChangeAsync("Hello");
    await Task.Delay(300);
    await WaitUntilAsync(() => docB!.Text == "Hello", TimeSpan.FromSeconds(5));

    // B appends
    await docB!.ApplyTextChangeAsync("Hello World");
    await Task.Delay(300);

    // Wait for convergence
    await WaitUntilAsync(() => docA.Text == "Hello World", TimeSpan.FromSeconds(5));

    docA.Text.Should().Be(docB.Text);
  }

  [Fact]
  public async Task ApplyItalicMarkOnA_BShouldReceiveItalicSpans()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);
    docB.Should().NotBeNull();

    await docA.ApplyTextChangeAsync("Hello World");
    await Task.Delay(300);
    await WaitUntilAsync(() => docB!.Text == "Hello World", TimeSpan.FromSeconds(5));

    // Apply italic mark on A ("World" chars 6-11)
    await docA.ApplyMarkAsync(6, 11, "italic", true, ExpandMode.Both);
    await Task.Delay(500);

    await WaitUntilAsync(
      () => docB!.FormattedText.Spans.Any(s => s.Marks.ContainsKey("italic")),
      TimeSpan.FromSeconds(5));

    docB!.FormattedText.Spans.Should().Contain(span => span.Marks.ContainsKey("italic"));
  }

  [Fact]
  public async Task MultipleMarksOnA_BShouldReceiveAllFormattingInfo()
  {
    using TextDocument docA = await TextDocument.CreateAsync(_repoA);
    using TextDocument? docB = await OpenWithRetryAsync(_repoB, docA.DocumentId);
    docB.Should().NotBeNull();

    await docA.ApplyTextChangeAsync("Hello World");
    await Task.Delay(300);
    await WaitUntilAsync(() => docB!.Text == "Hello World", TimeSpan.FromSeconds(5));

    // Apply bold + italic on A
    await docA.ApplyMarkAsync(0, 5, "bold", true, ExpandMode.Both);
    await docA.ApplyMarkAsync(0, 5, "italic", true, ExpandMode.Both);
    await Task.Delay(500);

    await WaitUntilAsync(
      () => docB!.FormattedText.Spans.Any(s => s.Marks.ContainsKey("bold") && s.Marks.ContainsKey("italic")),
      TimeSpan.FromSeconds(5));

    docB!.FormattedText.Spans.Should().Contain(span =>
      span.Marks.ContainsKey("bold") && span.Marks.ContainsKey("italic"));
  }

  /// <summary>
  /// Opens a TextDocument on a remote repo with retry logic.
  /// The sync protocol may deliver the empty document shell before the "text" field
  /// change arrives, so OpenAsync can initially return null. This helper retries.
  /// </summary>
  static async Task<TextDocument?> OpenWithRetryAsync(
    Repo repo, string documentId, TimeSpan? totalTimeout = null)
  {
    totalTimeout ??= TimeSpan.FromSeconds(5);
    var deadline = DateTime.UtcNow + totalTimeout.Value;

    while(DateTime.UtcNow < deadline)
    {
      TextDocument? doc = await TextDocument.OpenAsync(
        repo, documentId, timeout: TimeSpan.FromMilliseconds(500));

      if(doc is not null)
      {
        return doc;
      }

      await Task.Delay(100);
    }

    return null;
  }

  static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
  {
    var deadline = DateTime.UtcNow + timeout;
    while(!condition() && DateTime.UtcNow < deadline)
    {
      await Task.Delay(50);
    }

    condition().Should().BeTrue("condition should be met within timeout");
  }
}
