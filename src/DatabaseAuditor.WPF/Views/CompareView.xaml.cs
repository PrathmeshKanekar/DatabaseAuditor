namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

public partial class CompareView : UserControl
{
    private readonly CompareViewModel _viewModel;

    public CompareView(CompareViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.OnCompareCompleted = () =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Window.GetWindow(this) is MainWindow mw)
                    mw.NavigateToResults();
            });
        };

        Loaded += async (_, _) => await _viewModel.LoadConnectionsAsync();
    }
}