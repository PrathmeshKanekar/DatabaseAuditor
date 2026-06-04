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
        services.AddScoped<ICompareService, CompareService>();
        services.AddScoped<ConnectionService>();

        // Handlers
        services.AddScoped<CompareDatabaseHandler>();
        services.AddScoped<ExportReportHandler>();

        // Validators
        services.AddSingleton<ConnectionProfileValidator>();
        services.AddSingleton<CompareRequestValidator>();

        return services;
    }
}