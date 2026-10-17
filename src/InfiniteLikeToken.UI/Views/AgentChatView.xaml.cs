using System.Windows.Controls;

namespace InfiniteLikeToken.UI.Views;

public partial class AgentChatView : UserControl
{
    public AgentChatView()
    {
        InitializeComponent();
        
        // Auto-scroll to bottom whenever the content height changes (new messages or switching chats)
        this.Loaded += (s, e) => 
        {
            if (ChatScrollViewer != null)
            {
                ChatScrollViewer.ScrollChanged += (sender, args) =>
                {
                    // ExtentHeightChange triggers when text is added or a new chat list is loaded
                    if (args.ExtentHeightChange != 0)
                    {
                        ChatScrollViewer.ScrollToBottom();
                    }
                };
            }
        };
    }

    private void InputBox_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
    }

    private void InputBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is InfiniteLikeToken.UI.ViewModels.AgentChatViewModel vm)
            {
                vm.HandleFileDrop(files);
                e.Handled = true;
            }
        }
    }


    private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.V && System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                var fileList = System.Windows.Clipboard.GetFileDropList();
                string[] files = new string[fileList.Count];
                fileList.CopyTo(files, 0);

                if (DataContext is InfiniteLikeToken.UI.ViewModels.AgentChatViewModel vm)
                {
                    vm.HandleFileDrop(files);
                    e.Handled = true;
                }
            }
            else if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image != null)
                {
                    string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pasted_Image_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    using (var fileStream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                    {
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                        encoder.Save(fileStream);
                    }
                    if (DataContext is InfiniteLikeToken.UI.ViewModels.AgentChatViewModel vm)
                    {
                        vm.HandleFileDrop(new string[] { tempPath });
                        e.Handled = true;
                    }
                }
            }
        }
    }

}
