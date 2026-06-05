namespace DatabaseAuditor.Application;

using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Application.UseCases.CompareDatabase;
using DatabaseAuditor.Application.UseCases.ExportReport;
using DatabaseAuditor.Application.Validators;
using DatabaseAuditor.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services
        services.AddTransient<ICompareService, CompareService>();
        services.AddTransient<ConnectionService>();
        services.AddTransient<DatabaseObjectService>();

        // Handlers
        services.AddTransient<CompareDatabaseHandler>();
        services.AddTransient<ExportReportHandler>();

        // Validators
        services.AddSingleton<ConnectionProfileValidator>();
        services.AddSingleton<CompareRequestValidator>();

        return services;
    }
}
