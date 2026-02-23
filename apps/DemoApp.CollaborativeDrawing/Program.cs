// Copyright (c) Six. All rights reserved.

using Avalonia;
using Avalonia.ReactiveUI;
using DemoApp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Six.Avalonia.Mcp.Connector;

namespace DemoApp.CollaborativeDrawing;

sealed class Program
{
  [STAThread]
  public static void Main(string[] args)
  {
    IHost host = CreateHostBuilder(args).Build();
    App.Services = host.Services;

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
      .AddServiceDefaults()
#if DEBUG
      .AddAvaloniaMcpConnector(options => options.AppName = "CollaborativeDrawing")
#endif
      .ConfigureServices((context, services) =>
      {
        services.AddSingleton(new RepoServiceOptions
        {
          HubPath = "/hubs/drawing",
          AppName = "CollaborativeDrawing"
        });
        services.AddSingleton<RepoService>();
      });

  public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace()
      .UseReactiveUI();
}
