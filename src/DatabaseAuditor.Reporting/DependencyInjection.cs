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

        // Register orchestrating ReportService as the primary default IReportService
        services.AddSingleton<IReportService>(sp =>
            new ReportService(new IReportService[]
            {
                sp.GetRequiredService<ExcelReportGenerator>(),
                sp.GetRequiredService<PdfReportGenerator>()
            }));

        return services;
    }
}