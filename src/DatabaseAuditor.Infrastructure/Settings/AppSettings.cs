namespace DatabaseAuditor.Infrastructure.Settings;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string LogLevel { get; set; } = "Information";
    public string LogDirectory { get; set; } = string.Empty;
    public string DefaultExportDirectory { get; set; } = string.Empty;
    public bool AutoRefresh { get; set; } = false;
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
    public bool ShowUnchangedObjects { get; set; } = false;
    public int MaxParallelConnections { get; set; } = 4;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int CommandTimeoutSeconds { get; set; } = 120;
    public bool EnableAnimations { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
    public WindowSettings Window { get; set; } = new();
}

public class WindowSettings
{
    public double Width { get; set; } = 1400;
    public double Height { get; set; } = 900;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public bool IsMaximized { get; set; } = false;
}