namespace DatabaseAuditor.WPF;

using DatabaseAuditor.Application;
using DatabaseAuditor.Infrastructure;
using DatabaseAuditor.Infrastructure.Settings;
using DatabaseAuditor.Providers;
using DatabaseAuditor.Reporting;
using DatabaseAuditor.WPF.Helpers;
using DatabaseAuditor.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 1. Temporarily prevent shutdown on async yield since no windows are open yet
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(ConfigureServices)
                .Build();

            ServiceProvider = _host.Services;

            // Initialize settings and logging
            var settings = ServiceProvider.GetRequiredService<AppSettings>();
            Infrastructure.Logging.LoggerService.Initialize(settings);

            // Initialize theme
            var settingsRepo = ServiceProvider.GetRequiredService<SettingsRepository>();
            ThemeManager.Initialize(settingsRepo, settings.Theme);

            await _host.StartAsync();

            Log.Information("DatabaseAuditor starting. Version={Version}",
                System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString(3));

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();

            // 2. Restore normal shutdown mode and display the window
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LogStartupError(ex);
        }
    }

    private void LogStartupError(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logPath = Path.Combine(logDir, "startup.log");
            var errorText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FATAL STARTUP EXCEPTION:\n" +
                            $"{ex.GetType().FullName}: {ex.Message}\n" +
                            $"{ex.StackTrace}\n";
            if (ex.InnerException != null)
            {
                errorText += $"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n" +
                             $"{ex.InnerException.StackTrace}\n";
            }
            errorText += new string('-', 80) + "\n\n";

            File.AppendAllText(logPath, errorText);
            
            // Also attempt to log to Serilog if initialized
            if (Log.Logger != null)
            {
                Log.Fatal(ex, "[App] Fatal startup exception occurred");
                Log.CloseAndFlush();
            }
        }
        catch
        {
            // Fail silently on log writing, proceed to show messagebox
        }

        MessageBox.Show(
            $"A fatal error occurred during application startup:\n\n{ex.Message}\n\nCheck 'logs/startup.log' for details.",
            "Database Auditor — Fatal Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Shutdown(1);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("DatabaseAuditor shutting down.");
        Log.CloseAndFlush();

        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Layers
        services.AddInfrastructure();
        services.AddApplication();
        services.AddProviders();
        services.AddReporting();
        services.AddWpf();
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Log.Error(e.Exception, "[App] Unhandled dispatcher exception");
        }
        catch
        {
            // Ignore logging errors in unhandled exception handler
        }

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}",
            "Database Auditor — Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try
            {
                Log.Fatal(ex, "[App] Unhandled domain exception. IsTerminating={T}", e.IsTerminating);
                Log.CloseAndFlush();
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            Log.Error(e.Exception, "[App] Unobserved task exception");
        }
        catch
        {
            // Ignore logging errors
        }
        e.SetObserved();
    }
}
