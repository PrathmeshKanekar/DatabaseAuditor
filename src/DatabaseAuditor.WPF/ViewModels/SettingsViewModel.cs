namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Infrastructure.Settings;
using DatabaseAuditor.WPF.Helpers;
using Serilog;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly IDialogService _dialogService;
    private AppSettings _settings;

    [ObservableProperty] private string _selectedTheme = "Dark";
    [ObservableProperty] private string _selectedLogLevel = "Information";
    [ObservableProperty] private string _logDirectory = string.Empty;
    [ObservableProperty] private string _defaultExportDirectory = string.Empty;
    [ObservableProperty] private bool _autoRefresh;
    [ObservableProperty] private int _autoRefreshInterval = 30;
    [ObservableProperty] private bool _showUnchangedObjects;
    [ObservableProperty] private int _maxParallelConnections = 4;
    [ObservableProperty] private int _connectionTimeout = 30;
    [ObservableProperty] private int _commandTimeout = 120;
    [ObservableProperty] private bool _enableAnimations = true;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _hasChanges;

    public List<string> ThemeOptions { get; } = ["Dark", "Light"];
    public List<string> LogLevelOptions { get; } = ["Verbose", "Debug",
        "Information", "Warning", "Error"];
    public List<int> ParallelOptions { get; } = [1, 2, 4, 8, 16];
    public List<int> TimeoutOptions { get; } = [15, 30, 60, 120, 300];

    public string AppVersion => $"v{System.Reflection.Assembly
        .GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ThreeStarInfotech", "DatabaseAuditor");

    public SettingsViewModel(
        SettingsRepository settingsRepository,
        IDialogService dialogService,
        AppSettings settings)
    {
        _settingsRepository = settingsRepository;
        _dialogService = dialogService;
        _settings = settings;

        LoadFromSettings(settings);
    }

    partial void OnSelectedThemeChanged(string value) => MarkChanged();
    partial void OnSelectedLogLevelChanged(string value) => MarkChanged();
    partial void OnLogDirectoryChanged(string value) => MarkChanged();
    partial void OnDefaultExportDirectoryChanged(string value) => MarkChanged();
    partial void OnAutoRefreshChanged(bool value) => MarkChanged();
    partial void OnAutoRefreshIntervalChanged(int value) => MarkChanged();
    partial void OnShowUnchangedObjectsChanged(bool value) => MarkChanged();
    partial void OnMaxParallelConnectionsChanged(int value) => MarkChanged();
    partial void OnConnectionTimeoutChanged(int value) => MarkChanged();
    partial void OnCommandTimeoutChanged(int value) => MarkChanged();
    partial void OnEnableAnimationsChanged(bool value) => MarkChanged();

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        IsSaving = true;
        StatusMessage = string.Empty;

        try
        {
            _settings.Theme = SelectedTheme;
            _settings.LogLevel = SelectedLogLevel;
            _settings.LogDirectory = LogDirectory;
            _settings.DefaultExportDirectory = DefaultExportDirectory;
            _settings.AutoRefresh = AutoRefresh;
            _settings.AutoRefreshIntervalSeconds = AutoRefreshInterval;
            _settings.ShowUnchangedObjects = ShowUnchangedObjects;
            _settings.MaxParallelConnections = MaxParallelConnections;
            _settings.ConnectionTimeoutSeconds = ConnectionTimeout;
            _settings.CommandTimeoutSeconds = CommandTimeout;
            _settings.EnableAnimations = EnableAnimations;

            await _settingsRepository.SaveAsync(_settings);

            // Apply theme immediately
            if (Enum.TryParse<AppTheme>(SelectedTheme, out var theme))
                ThemeManager.SetTheme(theme);

            HasChanges = false;
            StatusMessage = "Settings saved successfully.";

            Log.Information("[Settings] Saved. Theme={Theme} LogLevel={Level}",
                SelectedTheme, SelectedLogLevel);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
            Log.Error(ex, "[Settings] Save failed");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var confirmed = _dialogService.ShowConfirmationAsync(
            "Reset Settings",
            "Reset all settings to defaults?").GetAwaiter().GetResult();

        if (!confirmed) return;

        LoadFromSettings(new AppSettings());
        StatusMessage = "Settings reset to defaults.";
        HasChanges = true;

        Log.Information("[Settings] Reset to defaults");
    }

    [RelayCommand]
    private void BrowseLogDirectory()
    {
        var path = _dialogService.ShowFolderBrowserDialog("Select Log Directory");
        if (!string.IsNullOrEmpty(path))
            LogDirectory = path;
    }

    [RelayCommand]
    private void BrowseExportDirectory()
    {
        var path = _dialogService.ShowFolderBrowserDialog("Select Export Directory");
        if (!string.IsNullOrEmpty(path))
            DefaultExportDirectory = path;
    }

    [RelayCommand]
    private void OpenAppDataFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppDataPath,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Open Failed", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var path = string.IsNullOrEmpty(LogDirectory) ? AppDataPath : LogDirectory;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Open Failed", ex.Message);
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        StatusMessage = string.Empty;
        HasChanges = false;
    }

    private void LoadFromSettings(AppSettings settings)
    {
        SelectedTheme = settings.Theme;
        SelectedLogLevel = settings.LogLevel;
        LogDirectory = settings.LogDirectory;
        DefaultExportDirectory = settings.DefaultExportDirectory;
        AutoRefresh = settings.AutoRefresh;
        AutoRefreshInterval = settings.AutoRefreshIntervalSeconds;
        ShowUnchangedObjects = settings.ShowUnchangedObjects;
        MaxParallelConnections = settings.MaxParallelConnections;
        ConnectionTimeout = settings.ConnectionTimeoutSeconds;
        CommandTimeout = settings.CommandTimeoutSeconds;
        EnableAnimations = settings.EnableAnimations;
    }

    private void MarkChanged() => HasChanges = true;
}