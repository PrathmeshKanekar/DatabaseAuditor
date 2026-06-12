namespace DatabaseAuditor.WPF;

using DatabaseAuditor.Application.Services;
using DatabaseAuditor.WPF.Helpers;
using DatabaseAuditor.WPF.ViewModels;
using DatabaseAuditor.WPF.ViewModels.Dialogs;
using DatabaseAuditor.WPF.Views;
using DatabaseAuditor.WPF.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddWpf(this IServiceCollection services)
    {
        // Services
        services.AddSingleton<IDialogService, DialogService>();

        // Main window
        services.AddSingleton<MainWindow>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ConnectionsViewModel>();
        services.AddSingleton<CompareViewModel>();
        services.AddSingleton<ResultsViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Dialog ViewModels (transient — new each time)
        services.AddTransient<AddConnectionViewModel>();

        // Views
        services.AddSingleton<DashboardView>();
        services.AddSingleton<ConnectionsView>();
        services.AddSingleton<CompareView>();
        services.AddSingleton<ResultsView>();
        services.AddSingleton<ReportsView>();
        services.AddSingleton<SettingsView>();

        return services;
    }
}