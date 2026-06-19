namespace DatabaseAuditor.Infrastructure.Settings;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class SettingsRepository
{
    private readonly string _filePath;
    private readonly object _syncLock = new();

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

    public AppSettings Load()
    {
        lock (_syncLock)
        {
            if (!File.Exists(_filePath))
            {
                var defaults = new AppSettings
                {
                    LogDirectory = GetDefaultLogDirectory(),
                    DefaultExportDirectory = Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments)
                };
                SaveUnsafe(defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                    ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }

    public Task<AppSettings> LoadAsync()
    {
        return Task.Run(() => Load());
    }

    public void Save(AppSettings settings)
    {
        lock (_syncLock)
        {
            SaveUnsafe(settings);
        }
    }

    public Task SaveAsync(AppSettings settings)
    {
        return Task.Run(() => Save(settings));
    }

    public async Task UpdateThemeAsync(string theme)
    {
        var settings = await LoadAsync().ConfigureAwait(false);
        settings.Theme = theme;
        await SaveAsync(settings).ConfigureAwait(false);
    }

    public async Task UpdateWindowAsync(WindowSettings window)
    {
        var settings = await LoadAsync().ConfigureAwait(false);
        settings.Window = window;
        await SaveAsync(settings).ConfigureAwait(false);
    }

    private void SaveUnsafe(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_filePath, json);
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