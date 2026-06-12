namespace DatabaseAuditor.WPF;

using DatabaseAuditor.Infrastructure.Settings;
using DatabaseAuditor.WPF.Controls;
using DatabaseAuditor.WPF.Helpers;
using DatabaseAuditor.WPF.ViewModels;
using DatabaseAuditor.WPF.Views;
using DatabaseAuditor.WPF.Views.Dialogs;
using DocumentFormat.OpenXml.Vml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsRepository _settingsRepository;
    private readonly DispatcherTimer _clockTimer;

    // Page instances (lazy)
    private DashboardView? _dashboardView;
    private ConnectionsView? _connectionsView;
    private CompareView? _compareView;
    private ResultsView? _resultsView;
    private ReportsView? _reportsView;
    private SettingsView? _settingsView;

    // Active nav button
    private Button? _activeNavButton;

    public MainWindow(
        MainViewModel viewModel,
        SettingsRepository settingsRepository)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settingsRepository = settingsRepository;
        DataContext = _viewModel;

        // Clock timer
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) =>
            ClockText.Text = DateTime.Now.ToString("HH:mm:ss  ddd, dd MMM yyyy");
        _clockTimer.Start();
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss  ddd, dd MMM yyyy");

        // Version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.ToString(3) ?? "2.0.0"}";

        // Restore window position/size
        RestoreWindowState();

        // Update theme toggle UI to match current theme
        UpdateThemeToggleUI();

        // Navigate to Dashboard initially
        NavigateTo(BtnDashboard, GetDashboardView(), "Dashboard");
    }

    // ─────────────────────────────────────────────
    // Navigation
    // ─────────────────────────────────────────────

    private void OnNavDashboard(object sender, RoutedEventArgs e)
        => NavigateTo(BtnDashboard, GetDashboardView(), "Dashboard");

    private void OnNavConnections(object sender, RoutedEventArgs e)
        => NavigateTo(BtnConnections, GetConnectionsView(), "Connections");

    private void OnNavCompare(object sender, RoutedEventArgs e)
        => NavigateTo(BtnCompare, GetCompareView(), "Compare Databases");

    private void OnNavResults(object sender, RoutedEventArgs e)
        => NavigateTo(BtnResults, GetResultsView(), "Comparison Results");

    private void OnNavReports(object sender, RoutedEventArgs e)
        => NavigateToReports();

    private void OnNavSettings(object sender, RoutedEventArgs e)
        => NavigateTo(BtnSettings, GetSettingsView(), "Settings");

    private void OnNavAbout(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog { Owner = this };
        dialog.ShowDialog();
    }

    public void NavigateToCompare()
        => NavigateTo(BtnCompare, GetCompareView(), "Compare Databases");

    public void NavigateToConnections()
        => NavigateTo(BtnConnections, GetConnectionsView(), "Connections");

    public void NavigateToReports()
    {
        NavigateTo(BtnReports, GetReportsView(), "Reports");
        if (_reportsView?.DataContext is ReportsViewModel rvm)
        {
            rvm.RefreshSession();
        }
    }

    private void NavigateTo(Button navButton, UIElement page, string title)
    {
        // Update active nav button styles
        if (_activeNavButton != null && _activeNavButton != navButton)
            _activeNavButton.Style = (System.Windows.Style)FindResource("NavButtonStyle");

        navButton.Style = (System.Windows.Style)FindResource("NavButtonActiveStyle");
        _activeNavButton = navButton;

        // Animate page in
        if (page is FrameworkElement fe)
        {
            fe.Opacity = 0;
            fe.RenderTransform = new TranslateTransform(0, 12);
        }

        PageHost.Content = page;
        PageTitleText.Text = title;

        if (page is FrameworkElement fe2)
        {
            var storyboard = new Storyboard();
            var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(220)));
            var slide = new DoubleAnimation(12, 0, new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(fade, fe2);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(slide, fe2);
            Storyboard.SetTargetProperty(slide,
                new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            storyboard.Children.Add(fade);
            storyboard.Children.Add(slide);
            storyboard.Begin();
        }

        SetStatus("Ready");
    }

    // ─────────────────────────────────────────────
    // Page Factory
    // ─────────────────────────────────────────────

    private DashboardView GetDashboardView()
        => _dashboardView ??= App.ServiceProvider.GetRequiredService<DashboardView>();

    private ConnectionsView GetConnectionsView()
        => _connectionsView ??= App.ServiceProvider.GetRequiredService<ConnectionsView>();

    private CompareView GetCompareView()
        => _compareView ??= App.ServiceProvider.GetRequiredService<CompareView>();

    private ResultsView GetResultsView()
        => _resultsView ??= App.ServiceProvider.GetRequiredService<ResultsView>();

    private ReportsView GetReportsView()
        => _reportsView ??= App.ServiceProvider.GetRequiredService<ReportsView>();

    private SettingsView GetSettingsView()
        => _settingsView ??= App.ServiceProvider.GetRequiredService<SettingsView>();

    // ─────────────────────────────────────────────
    // Theme Toggle — FIX: Shows proper light/dark label (not True/False)
    // ─────────────────────────────────────────────

    private void OnThemeToggleClick(object sender, MouseButtonEventArgs e)
    {
        ThemeManager.ToggleTheme();
        UpdateThemeToggleUI();
    }

    private void UpdateThemeToggleUI()
    {
        bool isDark = ThemeManager.IsDark;

        ThemeIcon.Text = isDark ? "🌙" : "☀";
        ThemeLabel.Text = isDark ? "Dark Mode" : "Light Mode";

        // Animate knob
        var anim = new DoubleAnimation
        {
            To = isDark ? 22 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        KnobTranslate.BeginAnimation(TranslateTransform.XProperty, anim);

        ThemeToggleBorder.Background = isDark
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("InputBorderBrush");
    }

    // ─────────────────────────────────────────────
    // Public helpers for child views
    // ─────────────────────────────────────────────

    public void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusDot.Fill = isError
            ? (Brush)FindResource("ErrorBrush")
            : (Brush)FindResource("SuccessBrush");
    }

    public void SetBusy(bool busy, string message = "Processing...")
    {
        TopBarSpinner.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy) SetStatus(message);
    }

    public void ShowSnackbar(string message, SnackbarType type = SnackbarType.Info)
        => Snackbar.Show(message, type);

    public void NavigateToResults()
    {
        NavigateTo(BtnResults, GetResultsView(), "Comparison Results");

        // Refresh results view
        if (_resultsView?.DataContext is ResultsViewModel rvm)
            _ = rvm.LoadFromSessionAsync();
    }

    // ─────────────────────────────────────────────
    // Window state
    // ─────────────────────────────────────────────

    private void RestoreWindowState()
    {
        try
        {
            var settings = App.ServiceProvider.GetRequiredService<AppSettings>();
            var ws = settings.Window;
            Left = ws.Left;
            Top = ws.Top;
            Width = ws.Width;
            Height = ws.Height;
            WindowState = ws.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }
        catch
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _clockTimer.Stop();

        try
        {
            var ws = new WindowSettings
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                IsMaximized = WindowState == WindowState.Maximized
            };

            _ = _settingsRepository.UpdateWindowAsync(ws);
        }
        catch { /* non-critical */ }
    }
}