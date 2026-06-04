namespace DatabaseAuditor.Application.UseCases.ExportReport;

using DatabaseAuditor.Domain.ValueObjects;

public class ExportReportCommand
{
    public CompareSession Session { get; set; } = null!;
    public bool ExportExcel { get; set; } = true;
    public bool ExportPdf { get; set; } = true;
    public string OutputDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
}