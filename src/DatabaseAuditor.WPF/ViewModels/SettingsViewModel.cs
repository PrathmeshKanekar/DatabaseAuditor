namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Infrastructure.Settings;
using DatabaseAuditor.WPF.Helpers;
using Microsoft.Extensions.Logging;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly IDialogService _dialogService;
    private AppSettings _settings;

    [ObservableProperty] private bool _showUnchangedObjects;
    [ObservableProperty] private int _connectionTimeoutSeconds;
    [ObservableProperty] private int _commandTimeoutSeconds;
    [ObservableProperty] private int _maxParallelConnections;
    [ObservableProperty] private string _logDirectory = string.Empty;
    [ObservableProperty] private string _defaultExportDirectory = string.Empty;
    [ObservableProperty] private string _logLevel = "Information";
    [ObservableProperty] private bool _enableAnimations;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public List<string> LogLevels { get; } =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

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

    private void LoadFromSettings(AppSettings s)
    {
        ShowUnchangedObjects = s.ShowUnchangedObjects;
        ConnectionTimeoutSeconds = s.ConnectionTimeoutSeconds;
        CommandTimeoutSeconds = s.CommandTimeoutSeconds;
        MaxParallelConnections = s.MaxParallelConnections;
        LogDirectory = s.LogDirectory;
        DefaultExportDirectory = s.DefaultExportDirectory;
        LogLevel = s.LogLevel;
        EnableAnimations = s.EnableAnimations;
    }

    [RelayCommand]
    public void BrowseLogDirectory()
    {
        var path = _dialogService.ShowFolderBrowserDialog("Select Log Directory");
        if (path != null) LogDirectory = path;
    }

    [RelayCommand]
    public void BrowseExportDirectory()
    {
        var path = _dialogService.ShowFolderBrowserDialog("Select Default Export Directory");
        if (path != null) DefaultExportDirectory = path;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        _settings.ShowUnchangedObjects = ShowUnchangedObjects;
        _settings.ConnectionTimeoutSeconds = ConnectionTimeoutSeconds;
        _settings.CommandTimeoutSeconds = CommandTimeoutSeconds;
        _settings.MaxParallelConnections = MaxParallelConnections;
        _settings.LogDirectory = LogDirectory;
        _settings.DefaultExportDirectory = DefaultExportDirectory;
        _settings.LogLevel = LogLevel;
        _settings.EnableAnimations = EnableAnimations;

        await _settingsRepository.SaveAsync(_settings);
        StatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    public async Task ResetDefaultsAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Reset Settings",
            "Reset all settings to defaults?");

        if (!confirm) return;

        _settings = new AppSettings();
        LoadFromSettings(_settings);
        await _settingsRepository.SaveAsync(_settings);
        StatusMessage = "Settings reset to defaults.";
    }
}