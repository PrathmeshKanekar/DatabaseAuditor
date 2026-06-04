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
using System.Windows;
using System.Windows.Threading;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException        += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException      += OnUnobservedTaskException;

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
        mainWindow.Show();
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
        Log.Error(e.Exception, "[App] Unhandled dispatcher exception");
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
            Log.Fatal(ex, "[App] Unhandled domain exception. IsTerminating={T}",
                e.IsTerminating);
    }

    private static void OnUnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "[App] Unobserved task exception");
        e.SetObserved();
    }
}
