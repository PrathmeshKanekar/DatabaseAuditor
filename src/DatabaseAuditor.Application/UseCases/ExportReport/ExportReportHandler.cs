namespace DatabaseAuditor.Application.UseCases.ExportReport;

using DatabaseAuditor.Domain.Interfaces;

public class ExportReportResult
{
    public string? ExcelPath { get; set; }
    public string? PdfPath { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ExportReportHandler
{
    private readonly IReportService _reportService;

    public ExportReportHandler(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<ExportReportResult> HandleAsync(
        ExportReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = new ExportReportResult();

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var baseName = $"DatabaseAudit_{timestamp}";

            var tasks = new List<Task>();

            string? excelPath = null;
            string? pdfPath = null;

            if (command.ExportExcel)
            {
                var path = Path.Combine(command.OutputDirectory, $"{baseName}.xlsx");
                tasks.Add(Task.Run(async () =>
                {
                    excelPath = await _reportService.GenerateExcelAsync(
                        command.Session, path, cancellationToken);
                }, cancellationToken));
            }

            if (command.ExportPdf)
            {
                var path = Path.Combine(command.OutputDirectory, $"{baseName}.pdf");
                tasks.Add(Task.Run(async () =>
                {
                    pdfPath = await _reportService.GeneratePdfAsync(
                        command.Session, path, cancellationToken);
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);

            result.ExcelPath = excelPath;
            result.PdfPath = pdfPath;
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}