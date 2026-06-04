namespace DatabaseAuditor.WPF.Helpers;

using DatabaseAuditor.Infrastructure.Settings;
using System.Windows;

public enum AppTheme
{
    Dark,
    Light
}

public static class ThemeManager
{
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    private static AppTheme _currentTheme = AppTheme.Dark;
    private static SettingsRepository? _settingsRepository;

    public static AppTheme CurrentTheme => _currentTheme;

    public static void Initialize(SettingsRepository settingsRepository, string savedTheme)
    {
        _settingsRepository = settingsRepository;

        _currentTheme = savedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Light
            : AppTheme.Dark;

        ApplyTheme(_currentTheme);
    }

    public static void ToggleTheme()
    {
        var newTheme = _currentTheme == AppTheme.Dark
            ? AppTheme.Light
            : AppTheme.Dark;

        SetTheme(newTheme);
    }

    public static void SetTheme(AppTheme theme)
    {
        if (_currentTheme == theme) return;

        _currentTheme = theme;
        ApplyTheme(theme);
        PersistTheme(theme);
    }

    private static void ApplyTheme(AppTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        // Remove existing theme dictionary
        var existing = dictionaries
            .FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("DarkTheme") ||
                 d.Source.OriginalString.Contains("LightTheme")));

        if (existing != null)
            dictionaries.Remove(existing);

        // Add new theme dictionary at position 0
        var uri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;

        var newDict = new ResourceDictionary
        {
            Source = new Uri(uri, UriKind.Relative)
        };

        dictionaries.Insert(0, newDict);
    }

    private static void PersistTheme(AppTheme theme)
    {
        if (_settingsRepository == null) return;

        Task.Run(async () =>
        {
            try
            {
                await _settingsRepository.UpdateThemeAsync(theme.ToString());
            }
            catch
            {
                // Non-critical — swallow persistence errors
            }
        });
    }

    public static bool IsDark => _currentTheme == AppTheme.Dark;
    public static bool IsLight => _currentTheme == AppTheme.Light;
}