namespace DatabaseAuditor.Infrastructure.Logging;

using DatabaseAuditor.Infrastructure.Settings;
using Serilog;
using Serilog.Events;

public static class LoggerService
{
    public static void Initialize(AppSettings settings)
    {
        var logDirectory = string.IsNullOrWhiteSpace(settings.LogDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ThreeStarInfotech",
                "DatabaseAuditor",
                "Logs")
            : settings.LogDirectory;

        if (!Directory.Exists(logDirectory))
            Directory.CreateDirectory(logDirectory);

        var logLevel = Enum.TryParse<LogEventLevel>(settings.LogLevel, true, out var level)
            ? level
            : LogEventLevel.Information;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProcessId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .WriteTo.File(
                path: Path.Combine(logDirectory, "errors-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                restrictedToMinimumLevel: LogEventLevel.Error,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        Log.Information("DatabaseAuditor logging initialized. LogLevel={LogLevel} LogDirectory={LogDirectory}",
            logLevel, logDirectory);
    }

    public static void LogConnection(string action, string connectionName, string databaseType)
        => Log.Information("[Connection] {Action} | Name={Name} | Type={Type}",
            action, connectionName, databaseType);

    public static void LogCompare(string source, string target, string compareType, TimeSpan duration)
        => Log.Information("[Compare] Source={Source} | Target={Target} | Type={Type} | Duration={Duration}ms",
            source, target, compareType, duration.TotalMilliseconds);

    public static void LogReportGeneration(string format, string outputPath, TimeSpan duration)
        => Log.Information("[Report] Format={Format} | Path={Path} | Duration={Duration}ms",
            format, outputPath, duration.TotalMilliseconds);

    public static void LogPerformance(string operation, long recordCount, TimeSpan duration)
        => Log.Information("[Performance] Operation={Operation} | Records={Records} | Duration={Duration}ms",
            operation, recordCount, duration.TotalMilliseconds);

    public static void LogError(string operation, Exception ex)
        => Log.Error(ex, "[Error] Operation={Operation} | Message={Message}",
            operation, ex.Message);

    public static void CloseAndFlush()
        => Log.CloseAndFlush();
}