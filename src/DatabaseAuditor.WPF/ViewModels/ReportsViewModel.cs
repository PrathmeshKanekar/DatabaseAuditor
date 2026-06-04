namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.UseCases.ExportReport;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.ValueObjects;
using DatabaseAuditor.WPF.Helpers;
using Serilog;
using System.IO;

public partial class ReportsViewModel : ObservableObject
{
    private readonly ExportReportHandler _exportHandler;
    private readonly IDialogService _dialogService;
    private CompareSession? _currentSession;

    [ObservableProperty]
    private bool _exportExcel = true;

    [ObservableProperty]
    private bool _exportPdf = true;

    [ObservableProperty]
    private string _outputDirectory = Environment.GetFolderPath(
        Environment.SpecialFolder.MyDocuments);

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _hasSession;

    [ObservableProperty]
    private string _sessionSummary = "No comparison session loaded.";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _lastExcelPath = string.Empty;

    [ObservableProperty]
    private string _lastPdfPath = string.Empty;

    [ObservableProperty]
    private bool _hasExcelOutput;

    [ObservableProperty]
    private bool _hasPdfOutput;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressMessage = string.Empty;

    // Session Stats
    [ObservableProperty] private int _totalObjects;
    [ObservableProperty] private int _addedCount;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private int _modifiedCount;
    [ObservableProperty] private string _sourceInfo = string.Empty;
    [ObservableProperty] private string _targetInfo = string.Empty;
    [ObservableProperty] private string _compareType = string.Empty;
    [ObservableProperty] private string _executionTime = string.Empty;

    public ReportsViewModel(
        ExportReportHandler exportHandler,
        IDialogService dialogService)
    {
        _exportHandler = exportHandler;
        _dialogService = dialogService;
    }

    public void LoadSession(CompareSession session)
    {
        _currentSession = session;
        HasSession = true;

        TotalObjects = session.TotalObjects;
        AddedCount = session.AddedCount;
        DeletedCount = session.DeletedCount;
        ModifiedCount = session.ModifiedCount;
        SourceInfo = session.Source.DisplayName;
        TargetInfo = session.Target.DisplayName;
        CompareType = session.CompareType.ToString();
        ExecutionTime = $"{session.ExecutionTime.TotalSeconds:F2}s";

        SessionSummary = $"{session.Source.DatabaseName} vs {session.Target.DatabaseName}" +
                         $"  |  {session.CompareType}" +
                         $"  |  {session.TotalObjects} objects";

        HasExcelOutput = false;
        HasPdfOutput = false;
        StatusMessage = string.Empty;
        HasError = false;

        Log.Information("[Reports] Session loaded: {Summary}", SessionSummary);
    }

    [RelayCommand]
    private void Refresh()
    {
        StatusMessage = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var selected = _dialogService.ShowFolderBrowserDialog(
            "Select Output Directory");

        if (!string.IsNullOrEmpty(selected))
        {
            OutputDirectory = selected;
            Log.Information("[Reports] Output directory set: {Path}", selected);
        }
    }

    [RelayCommand]
    private async Task GenerateReportsAsync()
    {
        if (_currentSession == null)
        {
            _dialogService.ShowInfo("No Session",
                "Please run a comparison first.");
            return;
        }

        if (!ExportExcel && !ExportPdf)
        {
            _dialogService.ShowInfo("No Format Selected",
                "Please select at least one report format.");
            return;
        }

        if (!Directory.Exists(OutputDirectory))
        {
            try { Directory.CreateDirectory(OutputDirectory); }
            catch
            {
                _dialogService.ShowError("Invalid Directory",
                    "Output directory could not be created.");
                return;
            }
        }

        IsGenerating = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        ProgressValue = 0;
        ProgressMessage = "Preparing report data...";

        try
        {
            var command = new ExportReportCommand
            {
                Session = _currentSession,
                ExportExcel = ExportExcel,
                ExportPdf = ExportPdf,
                OutputDirectory = OutputDirectory
            };

            ProgressValue = 20;
            ProgressMessage = "Generating report...";

            var result = await _exportHandler.HandleAsync(command);

            ProgressValue = 90;
            ProgressMessage = "Finalizing...";

            if (result.IsSuccess)
            {
                LastExcelPath = result.ExcelPath ?? string.Empty;
                LastPdfPath = result.PdfPath ?? string.Empty;
                HasExcelOutput = !string.IsNullOrEmpty(result.ExcelPath);
                HasPdfOutput = !string.IsNullOrEmpty(result.PdfPath);

                var generated = new List<string>();
                if (HasExcelOutput) generated.Add("Excel");
                if (HasPdfOutput) generated.Add("PDF");

                StatusMessage = $"Reports generated successfully: {string.Join(", ", generated)}";
                ProgressValue = 100;
                ProgressMessage = "Complete";

                Log.Information("[Reports] Generated: Excel={Excel} PDF={Pdf}",
                    result.ExcelPath, result.PdfPath);
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Report generation failed.";
                ProgressMessage = "Failed";

                Log.Error("[Reports] Generation failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            ProgressMessage = "Error";
            Log.Error(ex, "[Reports] Unexpected error during generation");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void OpenExcelReport()
    {
        if (string.IsNullOrEmpty(LastExcelPath) || !File.Exists(LastExcelPath))
        {
            _dialogService.ShowInfo("File Not Found",
                "Excel report file not found.");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = LastExcelPath,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Open Failed", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenPdfReport()
    {
        if (string.IsNullOrEmpty(LastPdfPath) || !File.Exists(LastPdfPath))
        {
            _dialogService.ShowInfo("File Not Found",
                "PDF report file not found.");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = LastPdfPath,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Open Failed", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenOutputDirectory()
    {
        if (!Directory.Exists(OutputDirectory))
        {
            _dialogService.ShowInfo("Directory Not Found",
                "Output directory does not exist.");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = OutputDirectory,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Open Failed", ex.Message);
        }
    }
}