using System;
using System.Windows;
using System.Windows.Input;

namespace InfiniteLikeTocken.UI.Helpers
{
    public class CopyToClipboardCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => parameter is string text && !string.IsNullOrWhiteSpace(text);
        
        public void Execute(object? parameter)
        {
            if (parameter is string text)
            {
                try 
                { 
                    Clipboard.SetText(text); 
                } 
                catch 
                { 
                    // Failsafe if another Windows process has the clipboard locked
                }
            }
        }
    }

    public static class ClipboardHelper
    {
        public static ICommand CopyCommand { get; } = new CopyToClipboardCommand();
    }
}
