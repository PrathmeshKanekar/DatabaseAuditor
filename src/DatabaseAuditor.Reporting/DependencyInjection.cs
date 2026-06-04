namespace DatabaseAuditor.Reporting;

using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Reporting.Excel;
using DatabaseAuditor.Reporting.Pdf;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        // Register individual generators
        services.AddSingleton<ExcelReportGenerator>();
        services.AddSingleton<PdfReportGenerator>();

        // Register both as IReportService for collection injection
        services.AddSingleton<IReportService, ExcelReportGenerator>();
        services.AddSingleton<IReportService, PdfReportGenerator>();

        // Register orchestrating ReportService
        services.AddSingleton<ReportService>();

        return services;
    }
}