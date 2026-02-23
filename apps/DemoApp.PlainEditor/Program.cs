// Copyright (c) Six. All rights reserved.

using Avalonia;
using Avalonia.ReactiveUI;
#if DEBUG
using Microsoft.Extensions.Hosting;
using Six.Avalonia.Mcp.Connector;
#endif

namespace DemoApp.PlainEditor;

sealed class Program
{
  [STAThread]
  public static void Main(string[] args)
  {
#if DEBUG
    IHost host = Host.CreateDefaultBuilder(args)
      .AddAvaloniaMcpConnector(options => options.AppName = "PlainEditor")
      .Build();
    _ = host.StartAsync();
#endif

    BuildAvaloniaApp()
      .StartWithClassicDesktopLifetime(args);
  }

  public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace()
      .UseReactiveUI();
}
