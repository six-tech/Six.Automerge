// Copyright (c) Six. All rights reserved.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DemoApp.CollaborativeDrawing.ViewModels;
using DemoApp.CollaborativeDrawing.Views;
using DemoApp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DemoApp.CollaborativeDrawing;

public partial class App : Application
{
  /// <summary>
  /// Gets or sets the DI service provider, set by Program.Main before Avalonia starts.
  /// </summary>
  public static IServiceProvider? Services { get; set; }

  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      var repoService = Services?.GetRequiredService<RepoService>();
      var loggerFactory = Services?.GetService<ILoggerFactory>();
      var vm = new CanvasViewModel(
        repoService,
        loggerFactory,
        action => Dispatcher.UIThread.Post(action));
      desktop.MainWindow = new MainWindow { DataContext = vm };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
