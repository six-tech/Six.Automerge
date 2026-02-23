#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property LangVersion=14

// Kill all running processes that belong to apps/ and infra/ projects.
//
// Finds processes by reading /proc/{pid}/cmdline and matching against
// directory names from apps/ (DemoApp.*, Server.Api, etc.) and infra/
// (Infra.AppHost, etc.). MSBuild and Roslyn workers are ignored.
// Kills the entire process tree for each match.
//
// Usage:
//   dotnet scripts/kill-apps.cs          # from repo root
//   dotnet kill-apps.cs                  # from scripts/

using System;
using System.Diagnostics;
using System.IO;

// Resolve repo root
var repoRoot = Path.GetFullPath(
  Directory.GetCurrentDirectory().EndsWith("scripts")
    ? ".."
    : ".");

var appsDir = Path.Combine(repoRoot, "apps");
var infraDir = Path.Combine(repoRoot, "infra");

// Collect app names from apps/ and infra/ directories
var appNames = Directory
  .GetDirectories(appsDir)
  .Concat(Directory.GetDirectories(infraDir))
  .Select(Path.GetFileName)
  .Where(n => n is not null)
  .Cast<string>()
  .ToArray();

Console.WriteLine($"Scanning for processes matching {appNames.Length} apps...");

var killed = 0;
var myPid = Environment.ProcessId;

foreach(var proc in Process.GetProcesses())
{
  try
  {
    if(proc.Id == myPid || proc.HasExited)
      continue;

    // Check command line on Linux, fall back to process name
    var cmdLine = TryReadCmdLine(proc.Id);
    if(cmdLine is null)
      continue;

    // Skip MSBuild/Roslyn build infrastructure
    if(cmdLine.Contains("MSBuild.dll") || cmdLine.Contains("VBCSCompiler"))
      continue;

    // Match against app directory names
    var matchedApp = appNames.FirstOrDefault(name =>
      cmdLine.Contains(name, StringComparison.OrdinalIgnoreCase));

    if(matchedApp is null)
      continue;

    Console.WriteLine($"  Killing PID {proc.Id,-8} [{matchedApp}]  {Truncate(cmdLine, 100)}");
    proc.Kill(entireProcessTree: true);
    killed++;
  }
  catch
  {
    // Process may have exited between enumeration and kill
  }
}

Console.WriteLine(killed > 0
  ? $"Killed {killed} process(es)."
  : "No matching processes found.");

static string? TryReadCmdLine(int pid)
{
  try
  {
    // Linux: /proc/{pid}/cmdline has null-separated args
    var path = $"/proc/{pid}/cmdline";
    if(File.Exists(path))
      return File.ReadAllText(path).Replace('\0', ' ').Trim();
  }
  catch { }

  return null;
}

static string Truncate(string s, int max) =>
  s.Length <= max ? s : s[..max] + "...";
