namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _viewModel;

    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        RefreshUI();
    }

    private void RefreshUI()
    {
        TotalConnectionsText.Text = _viewModel.TotalConnections.ToString();
        ProductionConnectionsText.Text = _viewModel.ProductionConnections.ToString();
        DevConnectionsText.Text = _viewModel.DevConnections.ToString();
        WelcomeDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");

        RecentConnectionsList.ItemsSource = _viewModel.RecentConnections;
        NoConnectionsText.Visibility = _viewModel.RecentConnections.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnQuickCompare(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateToCompare();
    }

    private void OnQuickConnections(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateToConnections();
    }

    private void OnQuickReports(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateToReports();
    }

    private void OnQuickResults(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.NavigateToResults();
    }
}