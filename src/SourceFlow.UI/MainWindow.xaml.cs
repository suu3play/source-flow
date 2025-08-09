using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SourceFlow.UI.ViewModels;

namespace SourceFlow.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetupKeyboardShortcuts();
    }

    private void SetupKeyboardShortcuts()
    {
        // F5キーでリフレッシュ
        InputBindings.Add(new KeyBinding(
            new RoutedCommand(),
            new KeyGesture(Key.F5)));

        // Ctrl+数字キーでタブ切り替え
        for (int i = 1; i <= 5; i++)
        {
            var keyGesture = new KeyGesture((Key)(Key.D0 + i), ModifierKeys.Control);
            var command = new RoutedCommand();
            var binding = new KeyBinding(command, keyGesture);
            InputBindings.Add(binding);
            CommandBindings.Add(new CommandBinding(command, (s, e) => NavigateToTabIndex(i - 1)));
        }
    }

    private void NavigateToTab(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagValue)
        {
            if (int.TryParse(tagValue, out int tabIndex))
            {
                NavigateToTabIndex(tabIndex);
            }
        }
    }

    private void NavigateToTabIndex(int tabIndex)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedTabIndex = tabIndex;
        }
    }
}