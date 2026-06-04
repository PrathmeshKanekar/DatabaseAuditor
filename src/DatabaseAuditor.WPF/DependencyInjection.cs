namespace DatabaseAuditor.WPF;

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
        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<CompareView>();
        services.AddTransient<ConnectionsView>();
        services.AddTransient<DashboardView>();
        services.AddTransient<ReportsView>();
        services.AddTransient<ResultsView>();
        services.AddTransient<SettingsView>();
        services.AddTransient<AboutDialog>();
        services.AddTransient<AddConnectionDialog>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CompareViewModel>();
        services.AddTransient<ConnectionsViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<ResultsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AddConnectionViewModel>();

        // Services
        services.AddSingleton<IDialogService, DialogService>();

        return services;
    }
}
