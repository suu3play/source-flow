using System.Windows.Controls;
using System.Windows.Input;
using SourceFlow.UI.ViewModels;

namespace SourceFlow.UI.Views;

public partial class FileManagerView : UserControl
{
    public FileManagerView()
    {
        InitializeComponent();
    }

    private void DirectoryTreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is FileManagerViewModel viewModel && e.NewValue is DirectoryItem selectedDirectory)
        {
            viewModel.SelectedDirectory = selectedDirectory;
        }
    }

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FileManagerViewModel viewModel && viewModel.SelectedFile != null)
        {
            if (viewModel.SelectedFile.IsDirectory)
            {
                // フォルダをダブルクリックした場合、そのフォルダを開く
                viewModel.OpenFolderCommand.Execute(null);
            }
        }
    }
}