#!/usr/bin/env dotnet
// =============================================================================
// Publish Script for Six.Automerge
// =============================================================================
//
// Full release orchestrator: builds, tests, packs, tags, creates GitHub
// releases, and publishes NuGet packages in a single pipeline.
//
// USAGE:
//   dotnet scripts/publish.cs [-- OPTIONS]
//
// OPTIONS:
//   --version <semver>        Version string (required, e.g. 1.2.3)
//   --feed <feed>             Target feed: pack-only, local, dev, staging, prod
//                             (default: pack-only)
//   --skip-tests              Skip running the test suite
//   --skip-native-build       Skip the native Rust build step
//   --create-release          Create a GitHub release and upload Rust artifacts
//   --dry-run                 Print what would happen without executing
//   -h, --help                Show this help message

#:sdk Microsoft.NET.Sdk

#:property TargetFramework=net10.0
#:property LangVersion=14
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property PublishAot=false

#:package CliWrap@3.6.6
#:package Spectre.Console@0.49.1

using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using Spectre.Console;

// Set working directory to solution root
if(Directory.GetCurrentDirectory().EndsWith("scripts"))
{
  Directory.SetCurrentDirectory("..");
}

var options = PublishOptions.Parse(args);

if(options.ShowHelp)
{
  PrintHelp();
  return 0;
}

// Validate required --version
if(string.IsNullOrWhiteSpace(options.Version))
{
  AnsiConsole.MarkupLine("[red]ERROR: --version is required. Example: --version 1.2.3[/]");
  return 1;
}

if(!IsValidSemVer(options.Version))
{
  AnsiConsole.MarkupLine("[red]ERROR: Invalid semver version '{0}'. Expected format: MAJOR.MINOR.PATCH[[‑prerelease]][/]", options.Version);
  return 1;
}

// Environment variables for NuGet feeds (same convention as pack.cs)
var localFeedUrl = Environment.GetEnvironmentVariable("NUGET_FEED_LOCAL_URL");
var devFeedUrl = Environment.GetEnvironmentVariable("NUGET_FEED_DEV_URL");
var devFeedPat = Environment.GetEnvironmentVariable("NUGET_FEED_DEV_PAT");
var stagingFeedUrl = Environment.GetEnvironmentVariable("NUGET_FEED_STAGING_URL");
var stagingFeedPat = Environment.GetEnvironmentVariable("NUGET_FEED_STAGING_PAT");
var productionFeedUrl = Environment.GetEnvironmentVariable("NUGET_FEED_PRODUCTION_URL");
var productionFeedPat = Environment.GetEnvironmentVariable("NUGET_FEED_PRODUCTION_PAT");

Console.WriteLine();
AnsiConsole.Write(new FigletText("Six Publisher").Color(Color.Green));
Console.WriteLine();

// Display configuration
var configTable = new Table()
  .Border(TableBorder.Rounded)
  .AddColumn("Setting")
  .AddColumn("Value");
configTable.AddRow("Version", options.Version);
configTable.AddRow("Feed", options.Feed.ToString());
configTable.AddRow("Skip Tests", options.SkipTests.ToString());
configTable.AddRow("Skip Native Build", options.SkipNativeBuild.ToString());
configTable.AddRow("Create Release", options.CreateRelease.ToString());
configTable.AddRow("Dry Run", options.DryRun.ToString());
AnsiConsole.Write(configTable);
Console.WriteLine();

// Validate environment for the target feed
var feedValidation = ValidateFeedEnvironment(options.Feed);
if(feedValidation != null)
{
  AnsiConsole.MarkupLine("[red]ERROR: {0}[/]", feedValidation);
  return 1;
}

var packagesDir = Path.Combine(Directory.GetCurrentDirectory(), "packages");

// ============================================================================
// Pipeline Execution
// ============================================================================

var exitCode = 0;

await AnsiConsole.Status()
  .Spinner(Spinner.Known.Dots)
  .StartAsync("Running publish pipeline...", async ctx =>
  {
    // Step 1: Build native library (optional)
    if(!options.SkipNativeBuild)
    {
      ctx.Status("Step 1/7: Building native Rust library...");
      AnsiConsole.Write(new Rule("[cyan]Step 1: Build Native Library[/]").LeftJustified());

      if(options.DryRun)
      {
        AnsiConsole.MarkupLine("  [yellow][[DRY RUN]][/] Would run: dotnet scripts/build-native.cs");
      }
      else
      {
        var result = await RunCommandAsync("dotnet", "scripts/build-native.cs");
        if(result != 0)
        {
          AnsiConsole.MarkupLine("[red]ERROR: Native build failed (exit code {0})[/]", result);
          exitCode = result;
          return;
        }
        AnsiConsole.MarkupLine("  [green]Native build completed[/]");
      }
      Console.WriteLine();
    }
    else
    {
      AnsiConsole.Write(new Rule("[dim]Step 1: Build Native Library (skipped)[/]").LeftJustified());
      Console.WriteLine();
    }

    // Step 2: dotnet build
    ctx.Status("Step 2/7: Building .NET projects...");
    AnsiConsole.Write(new Rule("[cyan]Step 2: Build .NET Projects[/]").LeftJustified());

    if(options.DryRun)
    {
      AnsiConsole.MarkupLine("  [yellow][[DRY RUN]][/] Would run: dotnet build --configuration Release -p:SkipRustBuild=true");
    }
    else
    {
      var result = await RunCommandAsync("dotnet", "build --configuration Release -p:SkipRustBuild=true");
      if(result != 0)
      {
        AnsiConsole.MarkupLine("[red]ERROR: .NET build failed (exit code {0})[/]", result);
        exitCode = result;
        return;
      }
      AnsiConsole.MarkupLine("  [green].NET build completed[/]");
    }
    Console.WriteLine();

    // Step 3: Run tests (optional)
    if(!options.SkipTests)
    {
      ctx.Status("Step 3/7: Running tests...");
      AnsiConsole.Write(new Rule("[cyan]Step 3: Run Tests[/]").LeftJustified());

      if(options.DryRun)
      {
        AnsiConsole.MarkupLine("  [yellow][[DRY RUN]][/] Would run: dotnet scripts/tests-run.cs");
      }
      else
      {
        var result = await RunCommandAsync("dotnet", "scripts/tests-run.cs");
        if(result != 0)
        {
          AnsiConsole.MarkupLine("[red]ERROR: Tests failed (exit code {0})[/]", result);
          exitCode = result;
          return;
        }
        AnsiConsole.MarkupLine("  [green]Tests passed[/]");
      }
      Console.WriteLine();
    }
    else
    {
      AnsiConsole.Write(new Rule("[dim]Step 3: Run Tests (skipped)[/]").LeftJustified());
      Console.WriteLine();
    }

    // Step 4: dotnet pack
    ctx.Status("Step 4/7: Packing NuGet packages...");
    AnsiConsole.Write(new Rule("[cyan]Step 4: Pack NuGet Packages[/]").LeftJustified());

    if(options.DryRun)
    {
      AnsiConsole.MarkupLine(
        "  [yellow][[DRY RUN]][/] Would run: dotnet pack --configuration Release -o packages/ -p:SkipRustBuild=true -p:Version={0}",
        options.Version);
    }
    else
    {
      Directory.CreateDirectory(packagesDir);
      var packArgs = $"pack --configuration Release -o \"{packagesDir}\" -p:SkipRustBuild=true -p:Version={options.Version}";
      var result = await RunCommandAsync("dotnet", packArgs);
      if(result != 0)
      {
        AnsiConsole.MarkupLine("[red]ERROR: Pack failed (exit code {0})[/]", result);
        exitCode = result;
        return;
      }

      // List created packages
      var packages = Directory.GetFiles(packagesDir, "*.nupkg");
      AnsiConsole.MarkupLine("  [green]Created {0} package(s):[/]", packages.Length);
      foreach(var pkg in packages)
      {
        AnsiConsole.MarkupLine("    [dim]{0}[/]", Path.GetFileName(pkg));
      }
    }
    Console.WriteLine();

    // Step 5: Git tag
    ctx.Status("Step 5/7: Creating git tag...");
    AnsiConsole.Write(new Rule("[cyan]Step 5: Git Tag[/]").LeftJustified());

    var tagName = $"v{options.Version}";

    if(options.DryRun)
    {
      AnsiConsole.MarkupLine("  [yellow][[DRY RUN]][/] Would run: git tag {0}", tagName);
    }
    else
    {
      var result = await RunCommandAsync("git", $"tag {tagName}");
      if(result != 0)
      {
        AnsiConsole.MarkupLine("[yellow]Warning: git tag failed (exit code {0}). Tag may already exist.[/]", result);
      }
      else
      {
        AnsiConsole.MarkupLine("  [green]Created tag: {0}[/]", tagName);
      }
    }
    Console.WriteLine();

    // Step 6: GitHub release (optional)
    if(options.CreateRelease)
    {
      ctx.Status("Step 6/7: Creating GitHub release...");
      AnsiConsole.Write(new Rule("[cyan]Step 6: GitHub Release[/]").LeftJustified());

      if(options.DryRun)
      {
        AnsiConsole.MarkupLine(
          "  [yellow][[DRY RUN]][/] Would run: gh release create {0} --title \"{0}\" --generate-notes",
          tagName);

        var artefactsRoot = Path.Combine(Directory.GetCurrentDirectory(), "artefacts-rust");
        if(Directory.Exists(artefactsRoot))
        {
          var assetFiles = Directory.GetFiles(artefactsRoot, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .ToArray();
          AnsiConsole.MarkupLine("  [yellow][[DRY RUN]][/] Would upload {0} asset(s) from artefacts-rust/", assetFiles.Length);
          foreach(var asset in assetFiles)
          {
            var relative = Path.GetRelativePath(artefactsRoot, asset);
            AnsiConsole.MarkupLine("    [dim]{0}[/]", relative);
          }
        }
      }
      else
      {
        // Create the release
        var ghResult = await RunCommandAsync("gh",
          $"release create {tagName} --title \"{tagName}\" --generate-notes");
        if(ghResult != 0)
        {
          AnsiConsole.MarkupLine("[red]ERROR: GitHub release creation failed (exit code {0})[/]", ghResult);
          exitCode = ghResult;
          return;
        }
        AnsiConsole.MarkupLine("  [green]Created release: {0}[/]", tagName);

        // Upload artifacts from artefacts-rust/
        var artefactsRoot = Path.Combine(Directory.GetCurrentDirectory(), "artefacts-rust");
        if(Directory.Exists(artefactsRoot))
        {
          var assetFiles = Directory.GetFiles(artefactsRoot, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .ToArray();

          if(assetFiles.Length > 0)
          {
            var assetArgs = string.Join(" ", assetFiles.Select(f => $"\"{f}\""));
            var uploadResult = await RunCommandAsync("gh",
              $"release upload {tagName} {assetArgs}");
            if(uploadResult != 0)
            {
              AnsiConsole.MarkupLine("[yellow]Warning: Some asset uploads may have failed[/]");
            }
            else
            {
              AnsiConsole.MarkupLine("  [green]Uploaded {0} asset(s)[/]", assetFiles.Length);
            }
          }
        }
      }
      Console.WriteLine();
    }
    else
    {
      AnsiConsole.Write(new Rule("[dim]Step 6: GitHub Release (skipped)[/]").LeftJustified());
      Console.WriteLine();
    }

    // Step 7: Push NuGet packages (if feed != pack-only)
    if(options.Feed != FeedTarget.PackOnly)
    {
      ctx.Status("Step 7/7: Publishing NuGet packages...");
      AnsiConsole.Write(new Rule("[cyan]Step 7: Publish NuGet Packages[/]").LeftJustified());

      if(options.DryRun)
      {
        var feedLabel = GetFeedLabel(options.Feed);
        AnsiConsole.MarkupLine("  [yellow][[DRY RUN]][/] Would push packages to {0}", feedLabel);

        if(Directory.Exists(packagesDir))
        {
          var packages = Directory.GetFiles(packagesDir, "*.nupkg")
            .Where(f => !f.EndsWith(".snupkg"))
            .ToArray();
          foreach(var pkg in packages)
          {
            AnsiConsole.MarkupLine("    [dim]{0}[/]", Path.GetFileName(pkg));
          }
        }
      }
      else
      {
        var pushResult = await PushPackagesAsync(options.Feed, packagesDir);
        if(pushResult != 0)
        {
          AnsiConsole.MarkupLine("[red]ERROR: NuGet push failed[/]");
          exitCode = pushResult;
          return;
        }
      }
      Console.WriteLine();
    }
    else
    {
      AnsiConsole.Write(new Rule("[dim]Step 7: Publish NuGet Packages (skipped — pack-only mode)[/]").LeftJustified());
      Console.WriteLine();
    }

    // Cleanup
    if(!options.DryRun && Directory.Exists(packagesDir))
    {
      ctx.Status("Cleaning up packages directory...");
      try
      {
        Directory.Delete(packagesDir, true);
        AnsiConsole.MarkupLine("[dim]Cleaned up packages/ directory[/]");
      }
      catch(Exception ex)
      {
        AnsiConsole.MarkupLine("[yellow]Warning: Could not clean up packages directory: {0}[/]",
          ex.Message.EscapeMarkup());
      }
    }
  });

// Final status
Console.WriteLine();
if(exitCode == 0)
{
  var suffix = options.DryRun ? " (dry run)" : "";
  AnsiConsole.Write(new Rule($"[green]Publish pipeline completed successfully{suffix}[/]").LeftJustified());
}
else
{
  AnsiConsole.Write(new Rule("[red]Publish pipeline failed[/]").LeftJustified());
}
Console.WriteLine();

return exitCode;

// ============================================================================
// Helper Methods
// ============================================================================

async Task<int> RunCommandAsync(string fileName, string arguments)
{
  var stdOut = new StringBuilder();
  var stdErr = new StringBuilder();

  var result = await Cli.Wrap(fileName)
    .WithArguments(arguments)
    .WithValidation(CommandResultValidation.None)
    .WithStandardOutputPipe(PipeTarget.Merge(
      PipeTarget.ToStringBuilder(stdOut),
      PipeTarget.ToDelegate(line => Console.WriteLine(line))))
    .WithStandardErrorPipe(PipeTarget.Merge(
      PipeTarget.ToStringBuilder(stdErr),
      PipeTarget.ToDelegate(line => Console.Error.WriteLine(line))))
    .ExecuteAsync();

  return result.ExitCode;
}

async Task<int> PushPackagesAsync(FeedTarget feed, string sourceDir)
{
  if(!Directory.Exists(sourceDir))
  {
    AnsiConsole.MarkupLine("[red]No packages directory found[/]");
    return 1;
  }

  var packages = Directory.GetFiles(sourceDir, "*.nupkg")
    .Where(f => !f.EndsWith(".snupkg"))
    .ToArray();

  if(packages.Length == 0)
  {
    AnsiConsole.MarkupLine("[yellow]No .nupkg files found to publish[/]");
    return 0;
  }

  if(feed == FeedTarget.Local)
  {
    // Local feed: copy files directly
    var feedPath = localFeedUrl!;
    Directory.CreateDirectory(feedPath);

    foreach(var pkg in packages)
    {
      var destPath = Path.Combine(feedPath, Path.GetFileName(pkg));
      File.Copy(pkg, destPath, overwrite: true);
      AnsiConsole.MarkupLine("  [green]Copied:[/] {0}", Path.GetFileName(pkg));

      // Also copy symbols if they exist
      var snupkg = Path.ChangeExtension(pkg, ".snupkg");
      if(File.Exists(snupkg))
      {
        var snupkgDest = Path.Combine(feedPath, Path.GetFileName(snupkg));
        File.Copy(snupkg, snupkgDest, overwrite: true);
        AnsiConsole.MarkupLine("  [green]Copied:[/] {0}", Path.GetFileName(snupkg));
      }
    }

    AnsiConsole.MarkupLine("  [green]Published {0} package(s) to local feed[/]", packages.Length);
    return 0;
  }

  // Remote feed: use dotnet nuget push
  var (feedUrl, apiKey) = GetFeedCredentials(feed);
  var failures = 0;

  foreach(var pkg in packages)
  {
    var pushResult = await Cli.Wrap("dotnet")
      .WithArguments($"nuget push \"{pkg}\" --skip-duplicate --api-key \"{apiKey}\" --source \"{feedUrl}\"")
      .WithValidation(CommandResultValidation.None)
      .ExecuteAsync();

    if(pushResult.ExitCode != 0)
    {
      AnsiConsole.MarkupLine("  [red]Failed to push: {0}[/]", Path.GetFileName(pkg));
      failures++;
    }
    else
    {
      AnsiConsole.MarkupLine("  [green]Pushed:[/] {0}", Path.GetFileName(pkg));
    }
  }

  if(failures > 0)
  {
    AnsiConsole.MarkupLine("[red]{0} package(s) failed to push[/]", failures);
    return 1;
  }

  AnsiConsole.MarkupLine("  [green]Published {0} package(s) to {1}[/]", packages.Length, GetFeedLabel(feed));
  return 0;
}

string? ValidateFeedEnvironment(FeedTarget feed) => feed switch
{
  FeedTarget.Local when string.IsNullOrEmpty(localFeedUrl) =>
    "NUGET_FEED_LOCAL_URL environment variable is required for local publishing",
  FeedTarget.Dev when string.IsNullOrEmpty(devFeedUrl) =>
    "NUGET_FEED_DEV_URL environment variable is required for dev feed publishing",
  FeedTarget.Dev when string.IsNullOrEmpty(devFeedPat) =>
    "NUGET_FEED_DEV_PAT environment variable is required for dev feed publishing",
  FeedTarget.Staging when string.IsNullOrEmpty(stagingFeedUrl) =>
    "NUGET_FEED_STAGING_URL environment variable is required for staging feed publishing",
  FeedTarget.Staging when string.IsNullOrEmpty(stagingFeedPat) =>
    "NUGET_FEED_STAGING_PAT environment variable is required for staging feed publishing",
  FeedTarget.Prod when string.IsNullOrEmpty(productionFeedUrl) =>
    "NUGET_FEED_PRODUCTION_URL environment variable is required for production feed publishing",
  FeedTarget.Prod when string.IsNullOrEmpty(productionFeedPat) =>
    "NUGET_FEED_PRODUCTION_PAT environment variable is required for production feed publishing",
  _ => null
};

(string Url, string ApiKey) GetFeedCredentials(FeedTarget feed) => feed switch
{
  FeedTarget.Dev => (devFeedUrl!, devFeedPat!),
  FeedTarget.Staging => (stagingFeedUrl!, stagingFeedPat!),
  FeedTarget.Prod => (productionFeedUrl!, productionFeedPat!),
  _ => throw new InvalidOperationException($"No credentials for feed: {feed}")
};

static string GetFeedLabel(FeedTarget feed) => feed switch
{
  FeedTarget.PackOnly => "pack-only",
  FeedTarget.Local => "LOCAL FEED",
  FeedTarget.Dev => "DEVELOPMENT FEED",
  FeedTarget.Staging => "STAGING FEED",
  FeedTarget.Prod => "PRODUCTION FEED",
  _ => feed.ToString()
};

static bool IsValidSemVer(string version) =>
  Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?(\+[a-zA-Z0-9.]+)?$");

static void PrintHelp()
{
  AnsiConsole.MarkupLine("[bold]Six.Automerge Publish Script[/]");
  Console.WriteLine();
  AnsiConsole.MarkupLine("Full release orchestrator: build, test, pack, tag, release, and publish.");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Usage:[/] dotnet scripts/publish.cs [[-- OPTIONS]]");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Options:[/]");
  AnsiConsole.MarkupLine("  --version <semver>        Version string (required, e.g. 1.2.3)");
  AnsiConsole.MarkupLine("  --feed <feed>             Target feed: pack-only, local, dev, staging, prod");
  AnsiConsole.MarkupLine("                            (default: pack-only)");
  AnsiConsole.MarkupLine("  --skip-tests              Skip running the test suite");
  AnsiConsole.MarkupLine("  --skip-native-build       Skip the native Rust build step");
  AnsiConsole.MarkupLine("  --create-release          Create a GitHub release and upload Rust artifacts");
  AnsiConsole.MarkupLine("  --dry-run                 Print what would happen without executing");
  AnsiConsole.MarkupLine("  -h, --help                Show this help message");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Feeds:[/]");
  AnsiConsole.MarkupLine("  pack-only                 Only create .nupkg files (no publish)");
  AnsiConsole.MarkupLine("  local                     Copy to local NuGet feed (NUGET_FEED_LOCAL_URL)");
  AnsiConsole.MarkupLine("  dev                       Push to development feed (NUGET_FEED_DEV_URL/PAT)");
  AnsiConsole.MarkupLine("  staging                   Push to staging feed (NUGET_FEED_STAGING_URL/PAT)");
  AnsiConsole.MarkupLine("  prod                      Push to production feed (NUGET_FEED_PRODUCTION_URL/PAT)");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Pipeline Steps:[/]");
  AnsiConsole.MarkupLine("  1. Build native Rust library          (skip with --skip-native-build)");
  AnsiConsole.MarkupLine("  2. Build .NET projects                (Release, SkipRustBuild=true)");
  AnsiConsole.MarkupLine("  3. Run tests                          (skip with --skip-tests)");
  AnsiConsole.MarkupLine("  4. Pack NuGet packages                (Version from --version)");
  AnsiConsole.MarkupLine("  5. Create git tag                     (v{version})");
  AnsiConsole.MarkupLine("  6. Create GitHub release              (with --create-release)");
  AnsiConsole.MarkupLine("  7. Push NuGet packages to feed        (with --feed != pack-only)");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Examples:[/]");
  AnsiConsole.MarkupLine("  dotnet scripts/publish.cs -- --version 1.0.0 --dry-run");
  AnsiConsole.MarkupLine("  dotnet scripts/publish.cs -- --version 1.0.0 --feed local --skip-tests");
  AnsiConsole.MarkupLine("  dotnet scripts/publish.cs -- --version 1.0.0 --feed prod --create-release");
}

// ============================================================================
// Options & Enums
// ============================================================================

enum FeedTarget
{
  PackOnly,
  Local,
  Dev,
  Staging,
  Prod
}

sealed class PublishOptions
{
  public string? Version { get; set; }
  public FeedTarget Feed { get; set; } = FeedTarget.PackOnly;
  public bool SkipTests { get; set; }
  public bool SkipNativeBuild { get; set; }
  public bool CreateRelease { get; set; }
  public bool DryRun { get; set; }
  public bool ShowHelp { get; set; }

  public static PublishOptions Parse(string[] arguments)
  {
    var opts = new PublishOptions();

    for(var i = 0; i < arguments.Length; i++)
    {
      var arg = arguments[i];

      switch(arg)
      {
        case "--version":
          opts.Version = GetValue(arguments, ref i);
          break;
        case "--feed":
          opts.Feed = ParseFeed(GetValue(arguments, ref i));
          break;
        case "--skip-tests":
          opts.SkipTests = true;
          break;
        case "--skip-native-build":
          opts.SkipNativeBuild = true;
          break;
        case "--create-release":
          opts.CreateRelease = true;
          break;
        case "--dry-run":
          opts.DryRun = true;
          break;
        case "-h":
        case "--help":
          opts.ShowHelp = true;
          break;
        default:
          if(arg.StartsWith('-'))
          {
            Console.WriteLine($"  Unknown argument ignored: {arg}");
          }
          break;
      }
    }

    return opts;
  }

  static FeedTarget ParseFeed(string value) => value.ToLowerInvariant() switch
  {
    "pack-only" => FeedTarget.PackOnly,
    "local" => FeedTarget.Local,
    "dev" or "development" => FeedTarget.Dev,
    "staging" => FeedTarget.Staging,
    "prod" or "production" => FeedTarget.Prod,
    _ => throw new ArgumentException($"Unknown feed target: {value}. Expected: pack-only, local, dev, staging, prod")
  };

  static string GetValue(string[] args, ref int index)
  {
    if(index + 1 >= args.Length)
    {
      throw new ArgumentException($"Missing value for {args[index]}");
    }

    index++;
    return args[index];
  }
}
