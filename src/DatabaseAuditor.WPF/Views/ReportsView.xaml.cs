namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows.Controls;

public partial class ReportsView : UserControl
{
    public ReportsView(ReportsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
