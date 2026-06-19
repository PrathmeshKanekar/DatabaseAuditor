using DatabaseAuditor.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DatabaseAuditor.WPF.Views;

public partial class ResultsView : UserControl
{
    private readonly ResultsViewModel _viewModel;

    public ResultsView(ResultsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void OnFilterTotal(object sender, MouseButtonEventArgs e)
    {
        _viewModel.FilterChangeType = "All";
    }

    private void OnFilterAdded(object sender, MouseButtonEventArgs e)
    {
        _viewModel.FilterChangeType = "Added";
    }

    private void OnFilterDeleted(object sender, MouseButtonEventArgs e)
    {
        _viewModel.FilterChangeType = "Deleted";
    }

    private void OnFilterModified(object sender, MouseButtonEventArgs e)
    {
        _viewModel.FilterChangeType = "Modified";
    }

    private void OnFilterUnchanged(object sender, MouseButtonEventArgs e)
    {
        _viewModel.FilterChangeType = "Unchanged";
    }

    private void OnResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.ShowDiffPanel)
        {
            DiffPanelCol.Width = new GridLength(500);
        }
        else
        {
            DiffPanelCol.Width = new GridLength(0);
        }
    }

    private void OnCloseDiffPanel(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedResult = null;
        _viewModel.ShowDiffPanel = false;
        DiffPanelCol.Width = new GridLength(0);
    }
}
