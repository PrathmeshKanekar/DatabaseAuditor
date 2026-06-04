namespace DatabaseAuditor.Infrastructure.Settings;

using System.Text.Json;

public class SettingsRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsRepository()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ThreeStarInfotech",
            "DatabaseAuditor",
            "settings.json");

        EnsureDirectoryExists();
    }

    public async Task<AppSettings> LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                var defaults = new AppSettings
                {
                    LogDirectory = GetDefaultLogDirectory(),
                    DefaultExportDirectory = Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments)
                };
                await SaveUnsafeAsync(defaults);
                return defaults;
            }

            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                ?? new AppSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            await SaveUnsafeAsync(settings);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateThemeAsync(string theme)
    {
        var settings = await LoadAsync();
        settings.Theme = theme;
        await SaveAsync(settings);
    }

    public async Task UpdateWindowAsync(WindowSettings window)
    {
        var settings = await LoadAsync();
        settings.Window = window;
        await SaveAsync(settings);
    }

    private async Task SaveUnsafeAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static string GetDefaultLogDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ThreeStarInfotech",
            "DatabaseAuditor",
            "Logs");

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}