// Copyright (c) Six. All rights reserved.

using ReactiveUI;

namespace DemoApp.PlainEditor.ViewModels;

public class EditorViewModel : ViewModelBase
{
  string _documentText = string.Empty;
  int _cursorPosition;
  int _selectionLength;

  public string DocumentText
  {
    get => _documentText;
    set => this.RaiseAndSetIfChanged(ref _documentText, value);
  }

  public int CursorPosition
  {
    get => _cursorPosition;
    set => this.RaiseAndSetIfChanged(ref _cursorPosition, value);
  }

  public int SelectionLength
  {
    get => _selectionLength;
    set => this.RaiseAndSetIfChanged(ref _selectionLength, value);
  }
}
