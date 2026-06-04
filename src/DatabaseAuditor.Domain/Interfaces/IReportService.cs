namespace DatabaseAuditor.Domain.Interfaces;

using DatabaseAuditor.Domain.ValueObjects;

public interface IReportService
{
    Task<string> GenerateExcelAsync(CompareSession session, string outputPath, CancellationToken cancellationToken = default);
    Task<string> GeneratePdfAsync(CompareSession session, string outputPath, CancellationToken cancellationToken = default);
}