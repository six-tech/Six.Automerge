#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property LangVersion=14

// Interactive menu that launches other scripts in this directory.
// Presents a selection prompt for building, testing, packing, and
// publishing — then delegates to the corresponding script.
//
// Usage:
//   dotnet scripts/runner.cs           # from repo root
//   dotnet runner.cs                   # from scripts/

#:package CliWrap@3.6.6
#:package Spectre.Console@0.49.1

using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;

// Set working directory to solution root
var currentDir = Directory.GetCurrentDirectory();
if (currentDir.EndsWith("scripts"))
{
    Directory.SetCurrentDirectory("..");
}

// Interactive test runner menu
AnsiConsole.Write(
    new FigletText("Six Runner")
        .LeftJustified()
        .Color(Color.Cyan1));

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("[grey]Powered by .NET 10 File-Based Apps[/]");
AnsiConsole.WriteLine();

var choice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("[green]What would you like to do?[/]")
        .PageSize(12)
        .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
        .AddChoices(new[] {
            "🔨 Build native Rust library",
            "📦 Pack NuGet Packages",
            "🚀 Publish release",
            "🧪 Run All Tests",
            "📊 Run Tests with Coverage",
            "📈 Generate Coverage Report",
            "🔍 Run Specific Test Project",
            "🧹 Clean Test Results",
            "❌ Exit"
        }));

switch (choice)
{
    case "🔨 Build native Rust library":
        await RunCommand("scripts/build-native.cs");
        break;

    case "📦 Pack NuGet Packages":
        await RunCommand("scripts/pack.cs");
        break;

    case "🚀 Publish release":
        await RunCommand("scripts/publish.cs");
        break;

    case "🧪 Run All Tests":
        await RunCommand("scripts/tests-run.cs");
        break;

    case "📊 Run Tests with Coverage":
        await RunCommand("scripts/tests-coverage.cs");
        break;

    case "📈 Generate Coverage Report":
        await RunCommand("scripts/tests-report.cs");
        break;

    case "🔍 Run Specific Test Project":
        await RunSpecificProject();
        break;

    case "🧹 Clean Test Results":
        await CleanTestResults();
        break;

    case "❌ Exit":
        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
        break;
}

async Task RunCommand(string scriptFile)
{
    if (!File.Exists(scriptFile))
    {
        AnsiConsole.MarkupLine($"[red]Script file '{scriptFile}' not found![/]");
        return;
    }

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync($"Running {scriptFile}...", async ctx =>
        {
            try
            {
                var result = await Cli.Wrap("dotnet")
                    .WithArguments($"run {scriptFile}")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();

                if (result.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine($"[red]Command failed with exit code {result.ExitCode}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        });
}

async Task RunSpecificProject()
{
    var testProjects = Directory.GetFiles(".", "*.Tests*.csproj", SearchOption.AllDirectories)
        .Where(p => !p.Contains("bin") && !p.Contains("obj"))
        .Select(p => Path.GetFileNameWithoutExtension(p))
        .ToArray();

    if (testProjects.Length == 0)
    {
        AnsiConsole.MarkupLine("[red]No test projects found![/]");
        return;
    }

    var project = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select a test project to run:[/]")
            .PageSize(10)
            .AddChoices(testProjects));

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync($"Running tests for {project}...", async ctx =>
        {
            try
            {
                var projectFile = Directory.GetFiles(".", $"{project}.csproj", SearchOption.AllDirectories)
                    .FirstOrDefault(p => !p.Contains("bin") && !p.Contains("obj"));

                if (projectFile == null)
                {
                    AnsiConsole.MarkupLine($"[red]Project file not found![/]");
                    return;
                }

                var withCoverage = AnsiConsole.Confirm("Run with code coverage?");
                
                var arguments = withCoverage
                    ? $"test \"{projectFile}\" --collect:\"XPlat Code Coverage\" --results-directory TestResults"
                    : $"test \"{projectFile}\"";

                var result = await Cli.Wrap("dotnet")
                    .WithArguments(arguments)
                    .ExecuteAsync();

                if (result.ExitCode == 0)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Tests passed for {project}[/]");
                    
                    if (withCoverage)
                    {
                        AnsiConsole.MarkupLine("[yellow]Coverage data saved to TestResults directory[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Tests failed for {project}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
        });
}

async Task CleanTestResults()
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Cleaning test results...", async ctx =>
        {
            try
            {
                var testResultsDirs = Directory.GetDirectories(".", "TestResults", SearchOption.AllDirectories)
                    .Where(d => !d.Contains("bin") && !d.Contains("obj"))
                    .ToArray();

                var coverageReportsDir = "coverage-reports";
                
                var dirsToClean = testResultsDirs.ToList();
                if (Directory.Exists(coverageReportsDir))
                {
                    dirsToClean.Add(coverageReportsDir);
                }

                if (dirsToClean.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No test results to clean[/]");
                    return;
                }

                foreach (var dir in dirsToClean)
                {
                    ctx.Status($"Deleting {dir}...");
                    Directory.Delete(dir, true);
                    AnsiConsole.MarkupLine($"[green]✓ Deleted {dir}[/]");
                }

                AnsiConsole.MarkupLine($"[green]Cleaned {dirsToClean.Count} directories[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
            
            await Task.CompletedTask; // Ensure async compilation
        });
}

// Ask if user wants to run another task
if (choice != "❌ Exit")
{
    AnsiConsole.WriteLine();
    if (AnsiConsole.Confirm("Would you like to run another task?"))
    {
        // Re-run the script
        await Cli.Wrap("dotnet")
            .WithArguments("run scripts/runner.cs")
            .ExecuteAsync();
    }
}
