// Copyright (c) Six. All rights reserved.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DemoApp.Shared;
using DemoApp.CollaborativeTaskBoard.ViewModels;
using DemoApp.CollaborativeTaskBoard.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DemoApp.CollaborativeTaskBoard;

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
      var vm = new BoardViewModel(
        repoService,
        dispatcher: action => Dispatcher.UIThread.Post(action));
      desktop.MainWindow = new MainWindow { DataContext = vm };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
