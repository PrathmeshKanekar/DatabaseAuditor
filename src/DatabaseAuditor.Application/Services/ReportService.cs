namespace DatabaseAuditor.Application.Services;

using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Domain.ValueObjects;

public class ReportService : IReportService
{
    private readonly IReportService _excelGenerator;
    private readonly IReportService _pdfGenerator;

    public ReportService(
        IEnumerable<IReportService> generators)
    {
        var list = generators.ToList();
        _excelGenerator = list.FirstOrDefault(g => g is not ReportService && g.GetType().Name.Contains("Excel"))
            ?? throw new InvalidOperationException("Excel report generator not registered.");
        _pdfGenerator = list.FirstOrDefault(g => g is not ReportService && g.GetType().Name.Contains("Pdf"))
            ?? throw new InvalidOperationException("PDF report generator not registered.");
    }

    public async Task<string> GenerateExcelAsync(
        CompareSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        EnsureDirectoryExists(outputPath);
        return await _excelGenerator.GenerateExcelAsync(session, outputPath, cancellationToken);
    }

    public async Task<string> GeneratePdfAsync(
        CompareSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        EnsureDirectoryExists(outputPath);
        return await _pdfGenerator.GeneratePdfAsync(session, outputPath, cancellationToken);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}