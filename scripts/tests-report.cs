#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk
#:property LangVersion 13

// Generate an HTML coverage report from the latest Cobertura XML
// produced by tests-coverage.cs. Uses ReportGenerator to create
// browsable HTML and opens it in the default browser.
//
// Prerequisites: run tests-coverage.cs first to collect coverage data.
//
// Usage:
//   dotnet scripts/tests-report.cs     # from repo root
//   dotnet tests-report.cs             # from scripts/

#:package CliWrap@3.6.6
#:package Spectre.Console@0.49.1
#:package ReportGenerator@5.3.11

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

// Generate detailed coverage reports
await AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .StartAsync("Generating coverage reports...", async ctx =>
    {
        try
        {
            // Find all coverage files in timestamped directories
            var coverageFiles = Directory.GetFiles(".", "coverage.cobertura.xml", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && f.Contains("TestResults"))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToArray();

            if (coverageFiles.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No coverage files found! Run 'dotnet run scripts/run-coverage.cs' first.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[green]Found {coverageFiles.Length} coverage file(s)[/]");
            
            // Use the latest coverage file's directory for HTML reports
            var latestCoverageFile = coverageFiles[0];
            var latestCoverageDir = Path.GetDirectoryName(latestCoverageFile)!;
            
            AnsiConsole.MarkupLine($"[yellow]Using latest coverage from: {Path.GetFileName(latestCoverageDir)}[/]");

            // HTML reports will be generated in the same timestamped directory
            var htmlReportsDir = Path.Combine(latestCoverageDir, "html-report");
            if (Directory.Exists(htmlReportsDir))
            {
                Directory.Delete(htmlReportsDir, true);
            }
            Directory.CreateDirectory(htmlReportsDir);

            // Generate HTML report using ReportGenerator
            ctx.Status("Generating HTML report...");
            
            var reportGeneratorPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget", "packages", "reportgenerator", "5.3.11", "tools", "net8.0", "ReportGenerator.dll"
            );

            if (!File.Exists(reportGeneratorPath))
            {
                // Try to restore the package first
                await Cli.Wrap("dotnet")
                    .WithArguments("tool install --global dotnet-reportgenerator-globaltool")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();

                // Use the global tool instead
                var result = await Cli.Wrap("reportgenerator")
                    .WithArguments($"-reports:\"{latestCoverageFile}\" -targetdir:\"{htmlReportsDir}\" -reporttypes:Html;Cobertura;JsonSummary")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();
            }
            else
            {
                var result = await Cli.Wrap("dotnet")
                    .WithArguments($"\"{reportGeneratorPath}\" -reports:\"{latestCoverageFile}\" -targetdir:\"{htmlReportsDir}\" -reporttypes:Html;Cobertura;JsonSummary")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();
            }

            // Parse and display coverage summary
            ctx.Status("Analyzing coverage data...");
            
            var summaryFile = Path.Combine(htmlReportsDir, "Summary.json");
            if (File.Exists(summaryFile))
            {
                var summaryJson = await File.ReadAllTextAsync(summaryFile);
                // Simple JSON parsing without additional dependencies
                var lineRateMatch = System.Text.RegularExpressions.Regex.Match(summaryJson, @"""linecoverage""\s*:\s*([\d.]+)");
                var branchRateMatch = System.Text.RegularExpressions.Regex.Match(summaryJson, @"""branchcoverage""\s*:\s*([\d.]+)");
                
                if (lineRateMatch.Success && branchRateMatch.Success)
                {
                    var lineRate = double.Parse(lineRateMatch.Groups[1].Value) / 100;
                    var branchRate = double.Parse(branchRateMatch.Groups[1].Value) / 100;
                    
                    DisplayCoverageSummary(lineRate, branchRate);
                }
            }
            else
            {
                // Fallback to parsing original Cobertura XML
                var doc = XDocument.Load(latestCoverageFile);
                var coverage = doc.Root;
                
                if (coverage != null)
                {
                    var lineRate = double.Parse(coverage.Attribute("line-rate")?.Value ?? "0");
                    var branchRate = double.Parse(coverage.Attribute("branch-rate")?.Value ?? "0");
                    
                    DisplayCoverageSummary(lineRate, branchRate);
                }
            }

            // Display report location
            var htmlReport = Path.Combine(htmlReportsDir, "index.html");
            if (File.Exists(htmlReport))
            {
                AnsiConsole.WriteLine();
                var timestampedDirName = Path.GetFileName(latestCoverageDir);
                var panel = new Panel($"[green]HTML Report Generated![/]\n\nTimestamp: [yellow]{timestampedDirName}[/]\nOpen in browser:\n[link]{Path.GetFullPath(htmlReport)}[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Green);
                AnsiConsole.Write(panel);
                
                // Try to open in default browser
                ctx.Status("Opening report in browser...");
                try
                {
                    await Cli.Wrap("cmd")
                        .WithArguments($"/c start \"\" \"{Path.GetFullPath(htmlReport)}\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync();
                }
                catch
                {
                    // Ignore if browser launch fails
                }
                
                // Clean up any temporary coverage-reports directory in solution root
                ctx.Status("Cleaning up temporary files...");
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
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            Environment.Exit(1);
        }
    });

void DisplayCoverageSummary(double lineRate, double branchRate)
{
    AnsiConsole.WriteLine();
    var rule = new Rule("[yellow]Coverage Summary[/]")
        .RuleStyle("grey")
        .LeftJustified();
    AnsiConsole.Write(rule);

    var chart = new BarChart()
        .Width(60)
        .Label("[green]Code Coverage[/]")
        .CenterLabel();

    var lineColor = lineRate >= 0.8 ? Color.Green : lineRate >= 0.6 ? Color.Yellow : Color.Red;
    var branchColor = branchRate >= 0.8 ? Color.Green : branchRate >= 0.6 ? Color.Yellow : Color.Red;

    chart.AddItem("Lines", lineRate * 100, lineColor);
    chart.AddItem("Branches", branchRate * 100, branchColor);

    AnsiConsole.Write(chart);

    // Display detailed metrics
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Metric")
        .AddColumn("Coverage")
        .AddColumn("Status");

    table.AddRow(
        "Line Coverage",
        $"{lineRate:P1}",
        lineRate >= 0.8 ? "[green]✓ Good[/]" : lineRate >= 0.6 ? "[yellow]⚠ Fair[/]" : "[red]✗ Poor[/]"
    );
    
    table.AddRow(
        "Branch Coverage",
        $"{branchRate:P1}",
        branchRate >= 0.8 ? "[green]✓ Good[/]" : branchRate >= 0.6 ? "[yellow]⚠ Fair[/]" : "[red]✗ Poor[/]"
    );

    AnsiConsole.Write(table);
}

AnsiConsole.MarkupLine("[green]Report generation completed![/]");
