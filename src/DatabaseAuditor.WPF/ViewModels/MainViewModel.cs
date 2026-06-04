namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.WPF.Helpers;
using Serilog;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPageName = "Dashboard";

    [ObservableProperty]
    private NavItem? _selectedItem;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isNavigationExpanded = true;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _activeNavItem = "Dashboard";

    // Nav items
    public List<NavItem> NavItems { get; } =
    [
        new() { Name = "Dashboard",          Icon = "⊞", PageKey = "Dashboard"   },
        new() { Name = "Connections",        Icon = "⛁", PageKey = "Connections" },
        new() { Name = "Database Compare",   Icon = "⇄", PageKey = "Compare"     },
        new() { Name = "Results",            Icon = "☰", PageKey = "Results"     },
        new() { Name = "Reports",            Icon = "📋", PageKey = "Reports"    },
        new() { Name = "Settings",           Icon = "⚙", PageKey = "Settings"   }
    ];

    private readonly CompareViewModel _compareViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly ResultsViewModel _resultsViewModel;
    private readonly ReportsViewModel _reportsViewModel;

    public MainViewModel(
        CompareViewModel compareViewModel,
        DashboardViewModel dashboardViewModel,
        ResultsViewModel resultsViewModel,
        ReportsViewModel reportsViewModel)
    {
        _isDarkTheme = ThemeManager.IsDark;
        _compareViewModel = compareViewModel;
        _dashboardViewModel = dashboardViewModel;
        _resultsViewModel = resultsViewModel;
        _reportsViewModel = reportsViewModel;

        _compareViewModel.CompareCompleted += OnCompareCompleted;

        // Initialize SelectedItem to the Dashboard item
        _selectedItem = NavItems.Find(n => n.PageKey == "Dashboard");
    }

    private void OnCompareCompleted(object? sender, DatabaseAuditor.Domain.ValueObjects.CompareSession session)
    {
        _dashboardViewModel.ApplySession(session);
        _resultsViewModel.LoadSession(session);
        _reportsViewModel.LoadSession(session);
        NavigateTo("Results");
    }

    partial void OnSelectedItemChanged(NavItem? value)
    {
        if (value != null)
        {
            NavigateTo(value.PageKey);
        }
    }

    partial void OnActiveNavItemChanged(string value)
    {
        CurrentPageName = value;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeManager.ToggleTheme();
        IsDarkTheme = ThemeManager.IsDark;
    }

    [RelayCommand]
    private void NavigateTo(string pageKey)
    {
        Log.Information("Navigation requested: {PageName}", pageKey);
        
        try
        {
            ActiveNavItem = pageKey;
            CurrentPageName = pageKey;

            // Sync SelectedItem if navigated programmatically (like on compare complete)
            var matchingItem = NavItems.Find(n => n.PageKey == pageKey);
            if (matchingItem != null && SelectedItem != matchingItem)
            {
                SelectedItem = matchingItem;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Navigation failed");
        }
    }

    [RelayCommand]
    private void ToggleNavigation()
        => IsNavigationExpanded = !IsNavigationExpanded;

    public void SetStatus(string message)
        => StatusMessage = message;

    public void SetLoading(bool loading)
        => IsLoading = loading;
}

public class NavItem
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string PageKey { get; set; } = string.Empty;
}