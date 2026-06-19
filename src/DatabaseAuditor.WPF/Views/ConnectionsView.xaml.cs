namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class ConnectionsView : UserControl
{
    private readonly ConnectionsViewModel _viewModel;

    public ConnectionsView(ConnectionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedConnection != null)
            _ = _viewModel.EditConnectionAsync();
    }
}