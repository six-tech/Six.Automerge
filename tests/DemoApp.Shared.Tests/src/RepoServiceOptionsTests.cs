// Copyright (c) Six. All rights reserved.

using DemoApp.Shared;
using FluentAssertions;
using Xunit;

namespace DemoApp.Shared.Tests;

[Trait("Category", "Unit")]
public class RepoServiceOptionsTests
{
  [Fact]
  public void Constructor_WithRequiredProperties_SetsValues()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      AppName = "TestApp"
    };

    options.HubPath.Should().Be("/hubs/text-editor");
    options.AppName.Should().Be("TestApp");
    options.HubUrl.Should().BeNull();
    options.StorageBaseDir.Should().BeNull();
  }

  [Fact]
  public void Constructor_WithStorageBaseDir_SetsCustomDir()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      AppName = "TestApp",
      StorageBaseDir = "/tmp/test-storage"
    };

    options.StorageBaseDir.Should().Be("/tmp/test-storage");
  }

  [Fact]
  public void DefaultStorageBaseDir_IsNull()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      AppName = "TestApp"
    };

    options.StorageBaseDir.Should().BeNull();
  }

  [Fact]
  public void HubUrl_WhenSet_OverridesAutomaticResolution()
  {
    var options = new RepoServiceOptions
    {
      HubPath = "/hubs/text-editor",
      HubUrl = "https://custom:9999/hubs/text-editor",
      AppName = "TestApp"
    };

    options.HubUrl.Should().Be("https://custom:9999/hubs/text-editor");
  }
}
