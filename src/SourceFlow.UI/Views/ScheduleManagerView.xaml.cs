using System.Windows.Controls;
using SourceFlow.UI.ViewModels;

namespace SourceFlow.UI.Views;

public partial class ScheduleManagerView : UserControl
{
    public ScheduleManagerView()
    {
        InitializeComponent();
    }

    public ScheduleManagerView(ScheduleManagerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}