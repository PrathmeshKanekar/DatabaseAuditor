namespace DatabaseAuditor.Reporting.Pdf;

using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Domain.ValueObjects;
using DatabaseAuditor.Reporting.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog;

public class PdfReportGenerator : IReportService
{
    private const string PrimaryColor = "#0D47A1";
    private const string AddedColor = "#2E7D32";
    private const string DeletedColor = "#C62828";
    private const string ModifiedColor = "#F57F17";
    private const string HeaderBg = "#1E3A5F";
    private const string LightGray = "#F5F5F5";
    private const string BorderColor = "#E0E0E0";

    static PdfReportGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<string> GenerateExcelAsync(
        CompareSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use ExcelReportGenerator for Excel output.");

    public async Task<string> GeneratePdfAsync(
        CompareSession session,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var model = ReportModel.FromSession(session);

            await Task.Run(() =>
            {
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                        page.Header().Element(c => BuildHeader(c, model));
                        page.Content().Element(c => BuildContent(c, model));
                        page.Footer().Element(BuildFooter);
                    });
                }).GeneratePdf(outputPath);

            }, cancellationToken);

            sw.Stop();
            Log.Information("[PDF] Report generated at {Path} in {Ms}ms",
                outputPath, sw.ElapsedMilliseconds);

            return outputPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[PDF] Failed to generate report at {Path}", outputPath);
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Header
    // -------------------------------------------------------------------------
    private static void BuildHeader(IContainer container, ReportModel model)
    {
        container
            .Background(HeaderBg)
            .Padding(12)
            .Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item()
                        .Text(model.Title)
                        .FontSize(18).Bold().FontColor(Colors.White);

                    col.Item()
                        .Text(model.CompanyName)
                        .FontSize(11).FontColor("#90CAF9");
                });

                row.ConstantItem(200).AlignRight().Column(col =>
                {
                    col.Item()
                        .Text($"Generated: {model.GeneratedAt:yyyy-MM-dd HH:mm}")
                        .FontSize(8).FontColor(Colors.White);

                    col.Item()
                        .Text($"By: {model.GeneratedBy}")
                        .FontSize(8).FontColor(Colors.White);

                    col.Item()
                        .Text($"Session: {model.SessionId.ToString()[..8].ToUpper()}")
                        .FontSize(8).FontColor("#90CAF9");
                });
            });
    }

    // -------------------------------------------------------------------------
    // Content
    // -------------------------------------------------------------------------
    private static void BuildContent(IContainer container, ReportModel model)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            // Summary Cards
            col.Item().Element(c => BuildSummaryCards(c, model));

            // Environment Info
            col.Item().Element(c => BuildEnvironmentSection(c, model));

            // Added Objects
            if (model.AddedObjects.Count > 0)
            {
                col.Item().Element(c => BuildResultTable(
                    c, "Added Objects",
                    model.AddedObjects,
                    AddedColor, "#E8F5E9"));
            }

            // Deleted Objects
            if (model.DeletedObjects.Count > 0)
            {
                col.Item().Element(c => BuildResultTable(
                    c, "Deleted Objects",
                    model.DeletedObjects,
                    DeletedColor, "#FFEBEE"));
            }

            // Modified Objects
            if (model.ModifiedObjects.Count > 0)
            {
                col.Item().Element(c => BuildModifiedTable(c, model));
            }
        });
    }

    // -------------------------------------------------------------------------
    // Summary Cards
    // -------------------------------------------------------------------------
    private static void BuildSummaryCards(IContainer container, ReportModel model)
    {
        container.Row(row =>
        {
            row.Spacing(8);

            BuildCard(row.RelativeItem(), "Total", model.TotalObjects, PrimaryColor, Colors.White);
            BuildCard(row.RelativeItem(), "Added", model.AddedCount, AddedColor, Colors.White);
            BuildCard(row.RelativeItem(), "Deleted", model.DeletedCount, DeletedColor, Colors.White);
            BuildCard(row.RelativeItem(), "Modified", model.ModifiedCount, ModifiedColor, Colors.White);
            BuildCard(row.RelativeItem(), "Unchanged", model.UnchangedCount, "#546E7A", Colors.White);
            BuildCard(row.RelativeItem(), "Duration",
                $"{model.ExecutionTime.TotalSeconds:F1}s", "#37474F", Colors.White);
        });
    }

    private static void BuildCard(
        IContainer container,
        string label,
        object value,
        string bgColor,
        string fgColor)
    {
        container
            .Background(bgColor)
            .Padding(10)
            .Column(col =>
            {
                col.Item()
                    .AlignCenter()
                    .Text(value.ToString() ?? "0")
                    .FontSize(22).Bold().FontColor(fgColor);

                col.Item()
                    .AlignCenter()
                    .Text(label)
                    .FontSize(9).FontColor(fgColor);
            });
    }

    // -------------------------------------------------------------------------
    // Environment Section
    // -------------------------------------------------------------------------
    private static void BuildEnvironmentSection(IContainer container, ReportModel model)
    {
        container.Row(row =>
        {
            row.Spacing(8);

            // Source
            row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item()
                    .Background(PrimaryColor).Padding(6)
                    .Text("Source Environment")
                    .Bold().FontColor(Colors.White).FontSize(9);

                col.Item().Padding(8).Column(info =>
                {
                    EnvRow(info, "Environment", model.SourceEnvironment);
                    EnvRow(info, "Database Type", model.SourceDatabaseType.ToString());
                    EnvRow(info, "Server", model.SourceServer);
                    EnvRow(info, "Database", model.SourceDatabase);
                });
            });

            // Target
            row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item()
                    .Background(PrimaryColor).Padding(6)
                    .Text("Target Environment")
                    .Bold().FontColor(Colors.White).FontSize(9);

                col.Item().Padding(8).Column(info =>
                {
                    EnvRow(info, "Environment", model.TargetEnvironment);
                    EnvRow(info, "Database Type", model.TargetDatabaseType.ToString());
                    EnvRow(info, "Server", model.TargetServer);
                    EnvRow(info, "Database", model.TargetDatabase);
                });
            });

            // Execution
            row.RelativeItem().Border(1).BorderColor(BorderColor).Column(col =>
            {
                col.Item()
                    .Background(PrimaryColor).Padding(6)
                    .Text("Execution Details")
                    .Bold().FontColor(Colors.White).FontSize(9);

                col.Item().Padding(8).Column(info =>
                {
                    EnvRow(info, "Compare Type", model.CompareType.ToString());
                    EnvRow(info, "Started At", model.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    EnvRow(info, "Completed At", model.CompletedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    EnvRow(info, "Status", model.IsSuccess ? "Success" : "Failed");
                });
            });
        });
    }

    private static void EnvRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(90)
                .Text(label).Bold().FontSize(8).FontColor("#546E7A");
            r.RelativeItem()
                .Text(value).FontSize(8);
        });
    }

    // -------------------------------------------------------------------------
    // Result Table (Added / Deleted)
    // -------------------------------------------------------------------------
    private static void BuildResultTable(
        IContainer container,
        string title,
        List<CompareResult> results,
        string accentColor,
        string headerBg)
    {
        container.Column(col =>
        {
            // Section Title
            col.Item()
                .Background(accentColor).Padding(6)
                .Text($"{title} ({results.Count})")
                .Bold().FontColor(Colors.White).FontSize(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(35);   // #
                    c.RelativeColumn(2);    // Object Type
                    c.RelativeColumn(2);    // Schema
                    c.RelativeColumn(4);    // Object Name
                    c.RelativeColumn(2);    // Change Type
                    c.RelativeColumn(3);    // Detected At
                });

                // Table Header
                table.Header(header =>
                {
                    TableHeaderCell(header, "#");
                    TableHeaderCell(header, "Object Type");
                    TableHeaderCell(header, "Schema");
                    TableHeaderCell(header, "Object Name");
                    TableHeaderCell(header, "Change Type");
                    TableHeaderCell(header, "Detected At");
                });

                // Rows
                int seq = 1;
                foreach (var r in results)
                {
                    var isAlt = seq % 2 == 0;
                    var bg = isAlt ? LightGray : Colors.White;

                    TableCell(table, seq.ToString(), bg);
                    TableCell(table, r.ObjectType.ToString(), bg);
                    TableCell(table, r.SchemaName, bg);
                    TableCell(table, r.ObjectName, bg);
                    TableCell(table, r.ChangeType.ToString(), bg, accentColor, true);
                    TableCell(table, r.DetectedAt.ToString("yyyy-MM-dd HH:mm"), bg);
                    seq++;
                }
            });
        });
    }

    // -------------------------------------------------------------------------
    // Modified Table
    // -------------------------------------------------------------------------
    private static void BuildModifiedTable(IContainer container, ReportModel model)
    {
        container.Column(col =>
        {
            col.Item()
                .Background(ModifiedColor).Padding(6)
                .Text($"Modified Objects ({model.ModifiedObjects.Count})")
                .Bold().FontColor(Colors.White).FontSize(10);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(35);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                    c.RelativeColumn(5);
                    c.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    TableHeaderCell(header, "#");
                    TableHeaderCell(header, "Object Type");
                    TableHeaderCell(header, "Schema");
                    TableHeaderCell(header, "Object Name");
                    TableHeaderCell(header, "Differences");
                    TableHeaderCell(header, "Detected At");
                });

                int seq = 1;
                foreach (var r in model.ModifiedObjects)
                {
                    var isAlt = seq % 2 == 0;
                    var bg = isAlt ? LightGray : Colors.White;
                    var diffs = string.Join("\n", r.Differences);

                    TableCell(table, seq.ToString(), bg);
                    TableCell(table, r.ObjectType.ToString(), bg);
                    TableCell(table, r.SchemaName, bg);
                    TableCell(table, r.ObjectName, bg);
                    TableCell(table, diffs, "#FFF8E1", ModifiedColor, false);
                    TableCell(table, r.DetectedAt.ToString("yyyy-MM-dd HH:mm"), bg);
                    seq++;
                }
            });
        });
    }

    // -------------------------------------------------------------------------
    // Footer
    // -------------------------------------------------------------------------
    private static void BuildFooter(IContainer container)
    {
        container
            .BorderTop(1).BorderColor(BorderColor)
            .PaddingTop(6)
            .Row(row =>
            {
                row.RelativeItem()
                    .Text("Database Auditor — Three Star Infotech")
                    .FontSize(8).FontColor("#9E9E9E");

                row.ConstantItem(100).AlignRight()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(8).FontColor("#9E9E9E");
                        x.CurrentPageNumber().FontSize(8).FontColor("#9E9E9E");
                        x.Span(" of ").FontSize(8).FontColor("#9E9E9E");
                        x.TotalPages().FontSize(8).FontColor("#9E9E9E");
                    });
            });
    }

    // -------------------------------------------------------------------------
    // Table Helpers
    // -------------------------------------------------------------------------
    private static void TableHeaderCell(TableCellDescriptor header, string text)
    {
        header.Cell()
            .Background(HeaderBg).Padding(5)
            .Text(text).Bold().FontColor(Colors.White).FontSize(8);
    }

    private static void TableCell(
        TableDescriptor table,
        string text,
        string bgColor,
        string? fontColor = null,
        bool bold = false)
    {
        var cell = table.Cell()
            .Background(bgColor)
            .BorderBottom(1).BorderColor(BorderColor)
            .Padding(4);

        var t = cell.Text(text).FontSize(8);

        if (fontColor != null) t.FontColor(fontColor);
        if (bold) t.Bold();
    }
}