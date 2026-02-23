#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property LangVersion 13

// Run all test projects with XPlat Code Coverage collection.
// Parses Cobertura XML results and displays line/branch coverage
// per project plus an overall summary.
//
// Usage:
//   dotnet scripts/tests-coverage.cs   # from repo root
//   dotnet tests-coverage.cs           # from scripts/

#:package CliWrap@3.6.6
#:package Spectre.Console@0.49.1

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;

// Set working directory to solution root
var currentDir = Directory.GetCurrentDirectory();
if (currentDir.EndsWith("scripts"))
{
    Directory.SetCurrentDirectory("..");
}

// Run tests with code coverage
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Setting up coverage run...", async ctx =>
    {
        try
        {
            // Clean previous test results
            var testResultsPath = Path.Combine("tests", "Six.KeycloakAdmin.Tests.Integration", "TestResults");
            if (Directory.Exists(testResultsPath))
            {
                ctx.Status("Cleaning previous test results...");
                Directory.Delete(testResultsPath, true);
            }

            // Find all test projects
            var testProjects = Directory.GetFiles("tests", "*.Tests*.csproj", SearchOption.AllDirectories)
                .Where(p => !p.Contains("bin") && !p.Contains("obj"))
                .ToArray();

            if (testProjects.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No test projects found![/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]Found {testProjects.Length} test project(s)[/]");
            AnsiConsole.WriteLine();

            var totalLinesCovered = 0;
            var totalLinesValid = 0;
            var totalBranchesCovered = 0;
            var totalBranchesValid = 0;

            foreach (var project in testProjects)
            {
                var projectName = Path.GetFileNameWithoutExtension(project);
                ctx.Status($"Running tests with coverage for [yellow]{projectName}[/]...");

                // Run tests with coverage (using default results directory)
                var projectDir = Path.GetDirectoryName(project) ?? ".";
                var testResultsDir = Path.Combine(projectDir, "TestResults");
                
                var result = await Cli.Wrap("dotnet")
                    .WithArguments($"test \"{project}\" --collect:\"XPlat Code Coverage\" --results-directory \"{testResultsDir}\" --verbosity minimal")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                if (result.ExitCode != 0)
                {
                    AnsiConsole.MarkupLine($"[red]Tests failed for {projectName}[/]");
                    continue;
                }

                // Find and parse coverage files, then move to timestamped directory
                var coverageFiles = Directory.GetFiles(testResultsDir, "coverage.cobertura.xml", SearchOption.AllDirectories);
                
                if (coverageFiles.Length > 0)
                {
                    // Create timestamped directory for this test run
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    var guid = Guid.NewGuid().ToString("N")[..8];
                    var timestampedDir = Path.Combine(testResultsDir, $"{timestamp}-{guid}");
                    Directory.CreateDirectory(timestampedDir);
                    
                    // Move coverage files to timestamped directory
                    foreach (var coverageFile in coverageFiles)
                    {
                        var originalDir = Path.GetDirectoryName(coverageFile)!;
                        var fileName = Path.GetFileName(coverageFile);
                        var newPath = Path.Combine(timestampedDir, fileName);
                        
                        // Copy coverage file
                        File.Copy(coverageFile, newPath, true);
                        
                        // Copy any other files from the original directory (json, etc.)
                        var otherFiles = Directory.GetFiles(originalDir, "*", SearchOption.TopDirectoryOnly)
                            .Where(f => !f.EndsWith("coverage.cobertura.xml"));
                        
                        foreach (var otherFile in otherFiles)
                        {
                            var otherFileName = Path.GetFileName(otherFile);
                            var otherNewPath = Path.Combine(timestampedDir, otherFileName);
                            File.Copy(otherFile, otherNewPath, true);
                        }
                        
                        // Clean up original directory
                        try
                        {
                            Directory.Delete(originalDir, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                    
                    // Update coverage files array to point to new location
                    coverageFiles = Directory.GetFiles(timestampedDir, "coverage.cobertura.xml", SearchOption.TopDirectoryOnly);
                }
                
                if (coverageFiles.Length > 0)
                {
                    var latestCoverage = coverageFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                    var doc = XDocument.Load(latestCoverage);
                    var coverage = doc.Root;

                    if (coverage != null)
                    {
                        var linesCovered = int.Parse(coverage.Attribute("lines-covered")?.Value ?? "0");
                        var linesValid = int.Parse(coverage.Attribute("lines-valid")?.Value ?? "0");
                        var branchesCovered = int.Parse(coverage.Attribute("branches-covered")?.Value ?? "0");
                        var branchesValid = int.Parse(coverage.Attribute("branches-valid")?.Value ?? "0");

                        totalLinesCovered += linesCovered;
                        totalLinesValid += linesValid;
                        totalBranchesCovered += branchesCovered;
                        totalBranchesValid += branchesValid;

                        var lineRate = double.Parse(coverage.Attribute("line-rate")?.Value ?? "0");
                        var branchRate = double.Parse(coverage.Attribute("branch-rate")?.Value ?? "0");

                        AnsiConsole.MarkupLine($"[cyan]{projectName}[/] - Lines: {lineRate:P1}, Branches: {branchRate:P1}");
                    }
                }
            }

            // Display overall coverage
            AnsiConsole.WriteLine();
            var rule = new Rule("[yellow]Overall Coverage Summary[/]")
                .RuleStyle("grey")
                .LeftJustified();
            AnsiConsole.Write(rule);

            var overallLineRate = totalLinesValid > 0 ? (double)totalLinesCovered / totalLinesValid : 0;
            var overallBranchRate = totalBranchesValid > 0 ? (double)totalBranchesCovered / totalBranchesValid : 0;

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Metric")
                .AddColumn("Coverage")
                .AddColumn("Details");

            var lineColor = overallLineRate >= 0.8 ? "green" : overallLineRate >= 0.6 ? "yellow" : "red";
            var branchColor = overallBranchRate >= 0.8 ? "green" : overallBranchRate >= 0.6 ? "yellow" : "red";

            table.AddRow(
                "Lines",
                $"[{lineColor}]{overallLineRate:P1}[/]",
                $"{totalLinesCovered}/{totalLinesValid}"
            );
            
            table.AddRow(
                "Branches",
                $"[{branchColor}]{overallBranchRate:P1}[/]",
                $"{totalBranchesCovered}/{totalBranchesValid}"
            );

            AnsiConsole.Write(table);

            // Recommendations
            AnsiConsole.WriteLine();
            if (overallLineRate < 0.8 || overallBranchRate < 0.8)
            {
                var panel = new Panel("[yellow]⚠ Coverage Recommendations:[/]\n" +
                    (overallLineRate < 0.8 ? "• Line coverage is below 80%. Consider adding more tests.\n" : "") +
                    (overallBranchRate < 0.8 ? "• Branch coverage is below 80%. Consider testing edge cases." : ""))
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Yellow);
                AnsiConsole.Write(panel);
            }
            else
            {
                var panel = new Panel("[green]✅ Excellent coverage! Keep up the good work.[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Green);
                AnsiConsole.Write(panel);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            Environment.Exit(1);
        }
    });

        // Clean up any temporary coverage-reports directory in solution root
        var tempCoverageReportsDir = "coverage-reports";
        if (Directory.Exists(tempCoverageReportsDir))
        {
            try
            {
                Directory.Delete(tempCoverageReportsDir, true);
                AnsiConsole.MarkupLine("[dim]Cleaned up temporary coverage-reports directory[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not clean up temporary directory: {ex.Message}[/]");
            }
        }
        
        AnsiConsole.MarkupLine("[green]Coverage run completed![/]");
