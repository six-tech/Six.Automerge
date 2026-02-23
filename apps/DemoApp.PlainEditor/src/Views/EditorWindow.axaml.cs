// Copyright (c) Six. All rights reserved.

using Avalonia.Controls;
using DemoApp.PlainEditor.ViewModels;

namespace DemoApp.PlainEditor.Views;

public partial class EditorWindow : Window
{
  public EditorWindow()
  {
    InitializeComponent();

    LocalEditor.PropertyChanged += (_, args) =>
    {
      if(DataContext is not EditorViewModel vm)
      {
        return;
      }

      if(args.Property == TextBox.CaretIndexProperty)
      {
        vm.CursorPosition = LocalEditor.CaretIndex;
      }

      if(args.Property == TextBox.SelectionStartProperty || args.Property == TextBox.SelectionEndProperty)
      {
        int start = LocalEditor.SelectionStart;
        int end = LocalEditor.SelectionEnd;
        vm.SelectionLength = Math.Abs(end - start);
      }
    };
  }
}
