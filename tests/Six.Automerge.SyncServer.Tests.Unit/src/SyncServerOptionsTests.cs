// Copyright (c) Six Technology, Inc. All rights reserved.
// Licensed under the MIT license.

using FluentAssertions;
using Six.Automerge.Repository.Storage;

namespace Six.Automerge.SyncServer.Tests.Unit;

[Trait("Category", "SyncServer")]
public sealed class SyncServerOptionsTests
{
  [Fact]
  public void Defaults_AreCorrect()
  {
    // Arrange & Act
    using var storage = new InMemoryStorageAdapter();
    var options = new SyncServerOptions { Storage = storage };

    // Assert
    options.PersistenceInterval.Should().Be(TimeSpan.FromSeconds(5));
    options.SyncDebounceRate.Should().Be(100);
    options.SaveDebounceRate.Should().Be(100);
    options.LoggerFactory.Should().BeNull();
  }

  [Fact]
  public void CustomValues_ArePreserved()
  {
    // Arrange & Act
    using var storage = new InMemoryStorageAdapter();
    var options = new SyncServerOptions
    {
      Storage = storage,
      PersistenceInterval = TimeSpan.FromSeconds(10),
      SyncDebounceRate = 200,
      SaveDebounceRate = 300
    };

    // Assert
    options.PersistenceInterval.Should().Be(TimeSpan.FromSeconds(10));
    options.SyncDebounceRate.Should().Be(200);
    options.SaveDebounceRate.Should().Be(300);
  }
}
