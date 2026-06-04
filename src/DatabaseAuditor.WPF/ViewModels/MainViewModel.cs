namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.WPF.Helpers;

public partial class MainViewModel : ObservableObject
{
    private readonly ThemeManager _themeManager;

    [ObservableProperty]
    private string _currentPageName = "Dashboard";

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

    public MainViewModel()
    {
        _isDarkTheme = ThemeManager.IsDark;
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
        ActiveNavItem = pageKey;
        CurrentPageName = pageKey;
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