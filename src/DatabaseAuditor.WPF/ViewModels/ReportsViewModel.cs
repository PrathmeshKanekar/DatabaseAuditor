namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.UseCases.ExportReport;
using DatabaseAuditor.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DatabaseAuditor.Domain.ValueObjects;

public partial class ReportsViewModel : ObservableObject
{
    private readonly ExportReportHandler _exportHandler;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private bool _exportExcel = true;
    [ObservableProperty] private bool _exportPdf = true;
    [ObservableProperty] private string _outputDirectory;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private string _sessionSummary = "No comparison session available.";

    [ObservableProperty] private string _sourceInfo = string.Empty;
    [ObservableProperty] private string _targetInfo = string.Empty;
    [ObservableProperty] private string _compareType = string.Empty;
    [ObservableProperty] private string _executionTime = string.Empty;
    [ObservableProperty] private string _comparisonScope = string.Empty;
    [ObservableProperty] private string _selectedObjectsSummary = string.Empty;
    [ObservableProperty] private int _totalObjects;
    [ObservableProperty] private int _addedCount;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private int _modifiedCount;

    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string _progressMessage = string.Empty;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private string? _lastExcelPath;
    [ObservableProperty] private string? _lastPdfPath;

    public bool HasExcelOutput => !string.IsNullOrEmpty(LastExcelPath);
    public bool HasPdfOutput => !string.IsNullOrEmpty(LastPdfPath);

    public ReportsViewModel(
        ExportReportHandler exportHandler,
        IDialogService dialogService)
    {
        _exportHandler = exportHandler;
        _dialogService = dialogService;
        _outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private CompareSession? GetActiveSession()
    {
        var baseSession = CompareViewModel.LastSession;
        if (baseSession == null) return null;

        if (ResultsViewModel.LastFilteredResults != null)
        {
            var filtered = ResultsViewModel.LastFilteredResults;
            return new CompareSession
            {
                Id = baseSession.Id,
                Source = baseSession.Source,
                Target = baseSession.Target,
                CompareType = baseSession.CompareType,
                ComparisonScope = baseSession.ComparisonScope,
                SelectedObjects = baseSession.SelectedObjects,
                StartedAt = baseSession.StartedAt,
                CompletedAt = baseSession.CompletedAt,
                IsSuccess = baseSession.IsSuccess,
                ErrorMessage = baseSession.ErrorMessage,
                Results = filtered
            };
        }

        return baseSession;
    }

    public void RefreshSession()
    {
        var session = GetActiveSession();
        HasSession = session != null;
        if (session != null)
        {
            SessionSummary = $"Session {session.Id.ToString()[..8].ToUpper()}";
            SourceInfo = $"[{session.Source.Environment}] {session.Source.Name}";
            TargetInfo = $"[{session.Target.Environment}] {session.Target.Name}";
            CompareType = session.CompareType.ToString();
            ExecutionTime = $"{session.ExecutionTime.TotalSeconds:F2}s";
            ComparisonScope = session.ComparisonScope.ToString();
            
            if (session.SelectedObjects != null && session.SelectedObjects.Count > 0)
            {
                SelectedObjectsSummary = session.SelectedObjects.Count <= 3
                    ? string.Join(", ", session.SelectedObjects)
                    : $"{string.Join(", ", session.SelectedObjects.Take(3))} ... (+{session.SelectedObjects.Count - 3} more)";
            }
            else
            {
                SelectedObjectsSummary = "All database objects";
            }

            TotalObjects = session.TotalObjects;
            AddedCount = session.AddedCount;
            DeletedCount = session.DeletedCount;
            ModifiedCount = session.ModifiedCount;
        }
    }

    [RelayCommand]
    public void BrowseOutputDirectory()
    {
        var path = _dialogService.ShowFolderBrowserDialog("Select Output Directory");
        if (path != null) OutputDirectory = path;
    }

    [RelayCommand]
    public void OpenExcelReport()
    {
        if (HasExcelOutput)
            OpenPath(LastExcelPath!);
    }

    [RelayCommand]
    public void OpenPdfReport()
    {
        if (HasPdfOutput)
            OpenPath(LastPdfPath!);
    }

    [RelayCommand]
    public void OpenOutputDirectory()
    {
        OpenPath(OutputDirectory);
    }

    private void OpenPath(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Error Opening File", $"Could not open path '{path}': {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task GenerateReportsAsync()
    {
        var session = GetActiveSession();
        if (session == null)
        {
            _dialogService.ShowInfo("No Session",
                "Please run a comparison first before exporting.");
            return;
        }

        if (!ExportExcel && !ExportPdf)
        {
            _dialogService.ShowInfo("Nothing to Export",
                "Please select at least one export format.");
            return;
        }

        IsGenerating = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        ProgressMessage = "Generating reports…";
        ProgressValue = 20;

        try
        {
            var command = new ExportReportCommand
            {
                Session = session,
                ExportExcel = ExportExcel,
                ExportPdf = ExportPdf,
                OutputDirectory = OutputDirectory
            };

            ProgressValue = 50;

            var result = await _exportHandler.HandleAsync(command);

            ProgressValue = 90;

            if (result.IsSuccess)
            {
                LastExcelPath = result.ExcelPath;
                LastPdfPath = result.PdfPath;
                OnPropertyChanged(nameof(HasExcelOutput));
                OnPropertyChanged(nameof(HasPdfOutput));
                
                var paths = new List<string>();
                if (result.ExcelPath != null) paths.Add($"Excel: {result.ExcelPath}");
                if (result.PdfPath != null) paths.Add($"PDF: {result.PdfPath}");
                
                StatusMessage = "Reports exported successfully.";
                ProgressValue = 100;
                
                _dialogService.ShowSuccess("Export Complete",
                    string.Join("\n", paths));
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Unknown error.";
                StatusMessage = $"Export failed: {result.ErrorMessage}";
                ProgressValue = 0;
                _dialogService.ShowError("Export Failed", result.ErrorMessage ?? "Unknown error.");
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = $"Error: {ex.Message}";
            ProgressValue = 0;
            _dialogService.ShowError("Export Error", ex.Message);
        }
        finally
        {
            IsGenerating = false;
        }
    }
}