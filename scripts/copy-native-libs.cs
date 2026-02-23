#!/usr/bin/env dotnet
// =============================================================================
// Copy Native Libraries Script for Six.Automerge
// =============================================================================
//
// Copies platform-specific native libraries from artefacts-rust/ to the
// specified output directory, stripping the platform suffix from filenames.
//
// USAGE:
//   dotnet scripts/copy-native-libs.cs [-- OPTIONS]
//
// OPTIONS:
//   --target-os <OS>       Target OS (windows, linux, macos). Auto-detects if omitted.
//   --target-arch <ARCH>   Target architecture (x64, arm64). Auto-detects if omitted.
//   -o, --output-dir <DIR> Output directory (required)
//   --artefacts-dir <DIR>  Override artefacts-rust/ location
//   -v, --verbose          Show detailed output
//   -h, --help             Show this help message

#:sdk Microsoft.NET.Sdk

#:property TargetFramework=net10.0
#:property LangVersion=14
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property PublishAot=false

#:package Spectre.Console@0.49.1

using System.Runtime.InteropServices;
using Spectre.Console;

const string NativeCrateName = "six_automerge";

var options = CopyOptions.Parse(args);

if(options.ShowHelp)
{
  PrintHelp();
  return 0;
}

if(string.IsNullOrWhiteSpace(options.OutputDir))
{
  AnsiConsole.MarkupLine("[red]ERROR: Output directory is required. Use --output-dir <path>[/]");
  return 1;
}

// Resolve artefacts directory
var artefactsRoot = options.ArtefactsDir;
if(string.IsNullOrWhiteSpace(artefactsRoot))
{
  var repoRoot = ResolveRepoRoot();
  artefactsRoot = Path.Combine(repoRoot, "artefacts-rust");
}

if(!Directory.Exists(artefactsRoot))
{
  AnsiConsole.MarkupLine("[red]ERROR: Artefacts directory not found:[/] {0}", artefactsRoot);
  AnsiConsole.MarkupLine("  Run 'dotnet scripts/build-native.cs' first to build the native library.");
  return 1;
}

// Detect platform
var platform = PlatformDetector.Detect(options.TargetOs, options.TargetArch);
var platformKey = $"{platform.OsName}-{platform.Architecture}";

if(options.Verbose)
{
  AnsiConsole.MarkupLine("  [grey]Platform:[/]     {0}", platformKey);
  AnsiConsole.MarkupLine("  [grey]Artefacts:[/]    {0}", artefactsRoot);
  AnsiConsole.MarkupLine("  [grey]Output dir:[/]   {0}", options.OutputDir);
}

// Ensure output directory exists
EnsureDirectoryExists(options.OutputDir);

// Find source artifacts
var sourceDir = Path.Combine(artefactsRoot, platformKey);
if(!Directory.Exists(sourceDir))
{
  AnsiConsole.MarkupLine("[yellow]Warning: No artifacts found for platform {0}[/]", platformKey);
  AnsiConsole.MarkupLine("  Run 'dotnet scripts/build-native.cs' to build the native library.");
  return 0;
}

// Source file: libsix_automerge-{os}-{arch}.{ext}
var sourceFileName = GetSourceFileName(NativeCrateName, platformKey, platform.OsName);
var sourcePath = Path.Combine(sourceDir, sourceFileName);

if(!File.Exists(sourcePath))
{
  AnsiConsole.MarkupLine("[yellow]Warning: Source file not found:[/] {0}", sourcePath);
  return 0;
}

var copiedCount = 0;

// Destination file: libsix_automerge.{ext} (platform suffix stripped)
var destFileName = GetDestFileName(NativeCrateName, platform.OsName);

// Copy to output directory
var destPath = Path.Combine(options.OutputDir, destFileName);
File.Copy(sourcePath, destPath, overwrite: true);
copiedCount++;

if(options.Verbose)
{
  AnsiConsole.MarkupLine("  [green]Copied:[/] {0} -> {1}", sourceFileName, destPath);
}

// Also copy to runtimes/{rid}/native/ for proper .NET resolution
var rid = platform.GetRuntimeIdentifier();
var runtimeNativeDir = Path.Combine(options.OutputDir, "runtimes", rid, "native");
EnsureDirectoryExists(runtimeNativeDir);

var runtimeDestPath = Path.Combine(runtimeNativeDir, destFileName);
File.Copy(sourcePath, runtimeDestPath, overwrite: true);

if(options.Verbose)
{
  AnsiConsole.MarkupLine("  [green]Copied:[/] {0} -> runtimes/{1}/native/{2}", sourceFileName, rid, destFileName);
}

if(!options.Verbose)
{
  AnsiConsole.MarkupLine("[green]Native libraries copied to output[/] ({0})", destFileName);
}

return 0;

// ============================================================================
// Helper Methods
// ============================================================================

static string GetSourceFileName(string crateName, string platformKey, string osName)
{
  var libName = crateName.Replace('-', '_');
  var ext = osName switch
  {
    "windows" => "dll",
    "macos" => "dylib",
    _ => "so"
  };

  return $"lib{libName}-{platformKey}.{ext}";
}

static string GetDestFileName(string crateName, string osName)
{
  var libName = crateName.Replace('-', '_');
  var ext = osName switch
  {
    "windows" => "dll",
    "macos" => "dylib",
    _ => "so"
  };

  return $"lib{libName}.{ext}";
}

static string ResolveRepoRoot()
{
  var cwd = Directory.GetCurrentDirectory();
  var dir = cwd;

  while(dir != null)
  {
    if(Directory.Exists(Path.Combine(dir, "artefacts-rust")))
    {
      return dir;
    }

    if(File.Exists(Path.Combine(dir, "CLAUDE.md")) &&
       Directory.Exists(Path.Combine(dir, "libs", "Six.Automerge.FFI")))
    {
      return dir;
    }

    dir = Path.GetDirectoryName(dir);
  }

  throw new InvalidOperationException(
    "Could not find repository root. Ensure artefacts-rust/ directory exists.");
}

static void EnsureDirectoryExists(string path)
{
  if(!Directory.Exists(path))
  {
    Directory.CreateDirectory(path);
  }
}

static void PrintHelp()
{
  AnsiConsole.MarkupLine("[bold]Six.Automerge Copy Native Libraries Script[/]");
  Console.WriteLine();
  AnsiConsole.MarkupLine("Copies platform-specific native libraries from artefacts-rust/ to output directory.");
  AnsiConsole.MarkupLine("Strips the platform suffix from filenames (e.g., libsix_automerge-linux-x64.so -> libsix_automerge.so).");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Usage:[/] dotnet scripts/copy-native-libs.cs [[-- OPTIONS]]");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Options:[/]");
  AnsiConsole.MarkupLine("  -o, --output-dir <DIR>    Output directory (required)");
  AnsiConsole.MarkupLine("  --target-os <OS>          Target OS: windows, linux, macos");
  AnsiConsole.MarkupLine("  --target-arch <ARCH>      Target architecture: x64, arm64");
  AnsiConsole.MarkupLine("  --artefacts-dir <DIR>     Override artefacts-rust/ location");
  AnsiConsole.MarkupLine("  -v, --verbose             Show detailed output");
  AnsiConsole.MarkupLine("  -h, --help                Show this help message");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Platform-to-RID Mapping:[/]");
  AnsiConsole.MarkupLine("  windows-x64   -> win-x64");
  AnsiConsole.MarkupLine("  windows-arm64 -> win-arm64");
  AnsiConsole.MarkupLine("  linux-x64     -> linux-x64");
  AnsiConsole.MarkupLine("  linux-arm64   -> linux-arm64");
  AnsiConsole.MarkupLine("  macos-x64     -> osx-x64");
  AnsiConsole.MarkupLine("  macos-arm64   -> osx-arm64");
  Console.WriteLine();
  AnsiConsole.MarkupLine("[bold]Examples:[/]");
  AnsiConsole.MarkupLine("  dotnet scripts/copy-native-libs.cs -- -o bin/Debug/net10.0");
  AnsiConsole.MarkupLine("  dotnet scripts/copy-native-libs.cs -- -o out --target-os macos --target-arch arm64");
}

// ============================================================================
// Options
// ============================================================================

sealed class CopyOptions
{
  public string OutputDir { get; set; } = string.Empty;
  public string? TargetOs { get; set; }
  public string? TargetArch { get; set; }
  public string? ArtefactsDir { get; set; }
  public bool Verbose { get; set; }
  public bool ShowHelp { get; set; }

  public static CopyOptions Parse(string[] arguments)
  {
    var options = new CopyOptions();

    for(var i = 0; i < arguments.Length; i++)
    {
      var arg = arguments[i];

      switch(arg)
      {
        case "-o":
        case "--output-dir":
          options.OutputDir = Path.GetFullPath(GetValue(arguments, ref i));
          break;
        case "--target-os":
          options.TargetOs = GetValue(arguments, ref i);
          break;
        case "--target-arch":
          options.TargetArch = GetValue(arguments, ref i);
          break;
        case "--artefacts-dir":
          options.ArtefactsDir = Path.GetFullPath(GetValue(arguments, ref i));
          break;
        case "-v":
        case "--verbose":
          options.Verbose = true;
          break;
        case "-h":
        case "--help":
          options.ShowHelp = true;
          break;
        default:
          if(arg.StartsWith('-'))
          {
            Console.WriteLine($"  Unknown argument ignored: {arg}");
          }
          break;
      }
    }

    return options;
  }

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

// ============================================================================
// Platform Detection
// ============================================================================

sealed class PlatformInfo
{
  public required string OsName { get; init; }
  public required string Architecture { get; init; }

  public string GetRuntimeIdentifier() => (OsName, Architecture) switch
  {
    ("windows", "x64") => "win-x64",
    ("windows", "arm64") => "win-arm64",
    ("macos", "x64") => "osx-x64",
    ("macos", "arm64") => "osx-arm64",
    ("linux", "x64") => "linux-x64",
    ("linux", "arm64") => "linux-arm64",
    _ => $"{OsName}-{Architecture}"
  };
}

static class PlatformDetector
{
  public static PlatformInfo Detect(string? cliTargetOs, string? cliTargetArch)
  {
    if(!string.IsNullOrWhiteSpace(cliTargetOs) && !string.IsNullOrWhiteSpace(cliTargetArch))
    {
      return new PlatformInfo
      {
        OsName = cliTargetOs.ToLowerInvariant(),
        Architecture = cliTargetArch.ToLowerInvariant()
      };
    }

    return new PlatformInfo
    {
      OsName = DetectOsName(),
      Architecture = DetectArchitecture()
    };
  }

  static string DetectOsName()
  {
    if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";

    if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";

    return "linux";
  }

  static string DetectArchitecture()
  {
    return RuntimeInformation.OSArchitecture switch
    {
      Architecture.X64 => "x64",
      Architecture.Arm64 => "arm64",
      _ => "x64"
    };
  }
}
