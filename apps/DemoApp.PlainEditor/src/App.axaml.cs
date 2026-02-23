// Copyright (c) Six. All rights reserved.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DemoApp.PlainEditor.ViewModels;
using DemoApp.PlainEditor.Views;

namespace DemoApp.PlainEditor;

public partial class App : Application
{
  public override void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted()
  {
    if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      var vm = new EditorViewModel();
      desktop.MainWindow = new EditorWindow { DataContext = vm };
    }

    base.OnFrameworkInitializationCompleted();
  }
}
