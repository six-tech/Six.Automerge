# CRITICAL
- Use `dotnet-csharp-coding-standards` skill for writing high quality C# code
- Use `dotnet-rust-csharp-ffi-bindings` skill for writing high quality C#-Rust bindings


## CRITICAL: ERORS AND ISSUES WHILE WRITING AND RUNNING CODE
If you experience that some errors are obstructing to build and/or run the code, ALWAYS FIX those erorrs. Do not report things like: "Those are errors from previous xxx, let my try something different". DO NOT EVER SKIP ERRORS THAT PREVENTS YOU TO DO THE TASK - FIX THEM!


## CRITICAL: APPLICATION TESTING REQUIREMENTS

These testing steps are MANDATORY. Do NOT skip them. They apply after every UI implementation task.

### Runtime Testing (after each UI feature or fix is complete)

You MUST test the running application using `six-avalonia-mcp` MCP tools:

1. Build and run the application (`dotnet run` or the project's configured launch command).
2. Connect to the running app via the `six-avalonia-mcp` MCP server.
3. Use these MCP tools to validate the change:
   - Capture a screenshot to visually verify the UI renders correctly.
   - Inspect the visual tree to confirm the expected control hierarchy.
   - Query element properties to verify bindings, data context, and state.
   - Simulate user interactions (click, type, navigate) to confirm behavior.
4. If `six-avalonia-mcp` tools are unavailable, inform the user and do NOT silently skip runtime testing.

## CRITICAL: CLIENT - SERVER DEVELOPMENT
- Do everything with Aspire `Infra.AppHost` as it orchestrates client and server apps
- When server and clients are running, use `six-avalonia-mcp` to use the client UI and then inspect traces, logs, console what happens both on client and server



## CRITICAL: USING SIX-AVALONIA-MCP

Each Avalonia application must use `Six.Avalonia.Mcp.Connector` to be able to connect and communicate with `Six.Avalonia.Mcp.Server`.

If `Six.Avalonia.Mcp.Connector` is not present in each Avalonia application:
- Install `Six.Avalonia.Mcp.Connector` version 1.0.6
- Register it with the application like this (example from some other app):
- 
```
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Six.Avalonia.Mcp.Connector;

namespace NoteVault.Desktop;

internal static class Program
{
  [STAThread]
  public static void Main(string[] args)
  {
    // IMPORTANT: Use IHostBuilder so Six.Avalonia.Mcp.Connector works correctly
    IHost host = CreateHostBuilder(args).Build();
    App.App.Services = host.Services;

    _ = host.StartAsync();

    BuildAvaloniaApp()
      .StartWithClassicDesktopLifetime(args);
  }

  public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
      .ConfigureLogging(logging =>
      {
        logging.ClearProviders();
        logging.AddDebug();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Trace);
      })
#if DEBUG
      .AddAvaloniaMcpConnector(options =>
      {
        options.AppName = "NameOfTheApp";
      })
#endif
      .ConfigureServices((context, services) =>
      {
        App.App.ConfigureServices(services);
      });

  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<NoteVault.App.App>()
      .UsePlatformDetect()
      .LogToTrace();
  }
}

```

## CRITICAL: ALWAYS NAME CONTROLS IN USER INTERFACES MADE WITH AVALONIA UI

- `six-avalonia-mcp` needs a way to have an easy access to controls. Therefore always name controls with: `Name` property in C# code or `x:Name` if control is made AXAML.
- When in `six-avalonia-mcp` session and you find control that you need to interact with, but control is not named, **set the name on the control instead doing workarounds.**


## CRITICAL: ORPHANED INSTANCES OF APPLICATION OR MCP SERVER

When starting a sprint that uses Avalonia UI and needs working on the UI using `six-avalonia-mcp`, check if there are lingering/unneded instances of previous application(s) runs and kill them (terminate processes).
Sometimes they can stay around hanging and then mcp has problems connecting.
**CRITICAL WHEN KILLING PROCESSES: **DO NOT KILL THE wig or some other needed processes~

## CRITICAL: FOR AVALONIA UI APPLICATIONS

For developing Avalonia UI applications, the use of `dotnet-avalonia-ui` skill is MANDATORY. Do NOT skip it!

