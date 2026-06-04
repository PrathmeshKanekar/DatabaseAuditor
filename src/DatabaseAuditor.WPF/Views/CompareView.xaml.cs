namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows.Controls;

public partial class CompareView : UserControl
{
    public CompareView(CompareViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (s, e) =>
        {
            if (viewModel.LoadCommand.CanExecute(null))
                await viewModel.LoadCommand.ExecuteAsync(null);
        };
    }
}
