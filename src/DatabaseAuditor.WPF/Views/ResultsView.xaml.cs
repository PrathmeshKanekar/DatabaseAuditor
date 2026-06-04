namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows.Controls;

public partial class ResultsView : UserControl
{
    public ResultsView(ResultsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
