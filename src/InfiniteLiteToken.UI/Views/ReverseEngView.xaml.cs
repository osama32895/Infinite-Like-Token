using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InfiniteLiteToken.UI.ViewModels;

namespace InfiniteLiteToken.UI.Views;

public partial class ReverseEngView : UserControl
{
    public ReverseEngView()
    {
        InitializeComponent();
    }

    private void OnTargetDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && DataContext is ReverseEngViewModel vm)
            {
                vm.LoadTarget(files[0]);
            }
        }
    }

    private void OnDropZoneClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ReverseEngViewModel vm)
        {
            vm.BrowseFile();
        }
    }
}
