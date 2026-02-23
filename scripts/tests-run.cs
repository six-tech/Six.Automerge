#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property LangVersion=14

// Discover and run all test projects, displaying per-test pass/fail
// results and a summary table.
//
// Usage:
//   dotnet scripts/tests-run.cs       # from repo root
//   dotnet tests-run.cs               # from scripts/

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

// Projects to ignore for testing
List<string> ignoredProjects = [];

// Run all tests in the solution
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Running tests...", async ctx =>
    {
        try
        {
            // Find all test projects (excluding ignored ones)
            var allTestProjects = Directory
                .GetFiles(".", "*.Tests*.csproj", SearchOption.AllDirectories)
                .Where(p => !p.Contains("bin") && !p.Contains("obj"))
                .ToArray();
            
            var testProjects = allTestProjects
                .Where(p => !ignoredProjects.Contains(Path.GetFileNameWithoutExtension(p)))
                .ToArray();

            if (testProjects.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No test projects found![/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]Found {testProjects.Length} test project(s)[/]");
            if (ignoredProjects.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Ignored {ignoredProjects.Count} non-test project(s): {string.Join(", ", ignoredProjects)}[/]");
            }
            AnsiConsole.WriteLine();

            var overallResults = new List<(string Project, bool Success, int Passed, int Failed, int Skipped, string Duration)>();

            foreach (var project in testProjects)
            {
                var projectName = Path.GetFileNameWithoutExtension(project);
                ctx.Status($"Running tests for [yellow]{projectName}[/]...");

                AnsiConsole.MarkupLine($"[cyan]🧪 Running tests for {projectName}...[/]");

                var result = await Cli.Wrap("dotnet")
                    .WithArguments($"test \"{project}\" --logger \"console;verbosity=normal\" --no-restore")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                // Parse detailed test results
                var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                // Show individual test results
                var testLines = lines.Where(l => 
                    l.Contains("Passed ") || 
                    l.Contains("Failed ") || 
                    l.Contains("Skipped ")).ToArray();

                foreach (var testLine in testLines)
                {
                    if (testLine.Contains("Passed "))
                    {
                        var testName = ExtractTestName(testLine);
                        AnsiConsole.MarkupLine($"  [green]✓[/] {testName}");
                    }
                    else if (testLine.Contains("Failed "))
                    {
                        var testName = ExtractTestName(testLine);
                        AnsiConsole.MarkupLine($"  [red]✗[/] {testName}");
                    }
                    else if (testLine.Contains("Skipped "))
                    {
                        var testName = ExtractTestName(testLine);
                        AnsiConsole.MarkupLine($"  [yellow]⊘[/] {testName}");
                    }
                }

                // Parse summary - count the actual test results we displayed
                var passed = testLines.Count(l => l.Contains("Passed "));
                var failed = testLines.Count(l => l.Contains("Failed "));
                var skipped = testLines.Count(l => l.Contains("Skipped "));
                
                // Try to extract duration from the output
                var durationLine = lines.FirstOrDefault(l => l.Contains("Duration:") || l.Contains("Total time:") || l.Contains("Test run for"));
                var duration = "N/A";
                
                if (durationLine != null)
                {
                    var durationMatch = System.Text.RegularExpressions.Regex.Match(durationLine, @"(\d+(?:\.\d+)?\s*[a-z]+)");
                    if (durationMatch.Success)
                    {
                        duration = durationMatch.Groups[1].Value.Trim();
                    }
                }
                
                // Fallback: look for summary line patterns
                var summaryLine = lines.FirstOrDefault(l => 
                    (l.Contains("Passed!") || l.Contains("Failed!") || l.Contains("Total tests:")) &&
                    (l.Contains("Passed:") || l.Contains("Failed:") || l.Contains("Skipped:")));
                
                if (summaryLine != null)
                {
                    var passedMatch = System.Text.RegularExpressions.Regex.Match(summaryLine, @"Passed:\s*(\d+)");
                    var failedMatch = System.Text.RegularExpressions.Regex.Match(summaryLine, @"Failed:\s*(\d+)");
                    var skippedMatch = System.Text.RegularExpressions.Regex.Match(summaryLine, @"Skipped:\s*(\d+)");
                    var durationMatch = System.Text.RegularExpressions.Regex.Match(summaryLine, @"Duration:\s*([^,\]]+)");

                    if (passedMatch.Success && int.TryParse(passedMatch.Groups[1].Value, out var summaryPassed) && summaryPassed > passed)
                        passed = summaryPassed;
                    if (failedMatch.Success && int.TryParse(failedMatch.Groups[1].Value, out var summaryFailed) && summaryFailed > failed)
                        failed = summaryFailed;
                    if (skippedMatch.Success && int.TryParse(skippedMatch.Groups[1].Value, out var summarySkipped) && summarySkipped > skipped)
                        skipped = summarySkipped;
                    if (durationMatch.Success) 
                        duration = durationMatch.Groups[1].Value.Trim();
                }

                overallResults.Add((projectName, result.ExitCode == 0, passed, failed, skipped, duration));
                
                var statusColor = result.ExitCode == 0 ? "green" : "red";
                var statusIcon = result.ExitCode == 0 ? "✓" : "✗";
                var total = passed + failed + skipped;
                
                AnsiConsole.MarkupLine($"[{statusColor}]{statusIcon} {projectName}: {total} tests ({passed} passed, {failed} failed, {skipped} skipped) in {duration}[/]");
                AnsiConsole.WriteLine();
            }

            // Display summary table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Project")
                .AddColumn("Status")
                .AddColumn("Passed")
                .AddColumn("Failed") 
                .AddColumn("Skipped")
                .AddColumn("Total")
                .AddColumn("Duration");

            foreach (var (project, success, passed, failed, skipped, duration) in overallResults)
            {
                var status = success ? "[green]✓ Passed[/]" : "[red]✗ Failed[/]";
                var total = passed + failed + skipped;
                
                table.AddRow(
                    $"[cyan]{project}[/]",
                    status,
                    passed.ToString(),
                    failed > 0 ? $"[red]{failed}[/]" : failed.ToString(),
                    skipped > 0 ? $"[yellow]{skipped}[/]" : skipped.ToString(),
                    total.ToString(),
                    duration
                );
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            Environment.Exit(1);
        }
    });

AnsiConsole.MarkupLine("[green]Test run completed![/]");


// Helper function to extract test name from dotnet test output
string ExtractTestName(string testLine)
{
    // Test lines typically look like:
    // "  Passed TokenCache_GetAsync_WithValidToken_ReturnsToken [2 ms]"
    // "  Failed TokenProvider_GetTokenAsync_WhenTokenExpires_RequestsNewToken [< 1 ms]"
    
    var parts = testLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 2)
    {
        // Skip the status word (Passed/Failed/Skipped) and take the test name
        var testName = parts[1];
        
        // Remove any timing information in brackets
        var bracketIndex = testName.IndexOf('[');
        if (bracketIndex > 0)
        {
            testName = testName.Substring(0, bracketIndex).Trim();
        }
        
        return testName;
    }
    
    return testLine.Trim();
}
