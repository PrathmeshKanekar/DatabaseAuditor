namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.ValueObjects;
using DatabaseAuditor.WPF.Helpers;
using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.DataSourceVersioning;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

public partial class ResultsViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private CompareSession? _currentSession;
    private List<CompareResult> _allResults = [];

    [ObservableProperty]
    private ObservableCollection<CompareResult> _results = [];

    [ObservableProperty]
    private ICollectionView? _resultsView;

    [ObservableProperty]
    private CompareResult? _selectedResult;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showAdded = true;

    [ObservableProperty]
    private bool _showDeleted = true;

    [ObservableProperty]
    private bool _showModified = true;

    [ObservableProperty]
    private bool _showUnchanged = false;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _addedCount;

    [ObservableProperty]
    private int _deletedCount;

    [ObservableProperty]
    private int _modifiedCount;

    [ObservableProperty]
    private int _unchangedCount;

    [ObservableProperty]
    private int _filteredCount;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _sessionInfo = string.Empty;

    [ObservableProperty]
    private string _executionTime = string.Empty;

    public event EventHandler? ExportRequested;

    public ResultsViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnShowAddedChanged(bool value) => ApplyFilter();
    partial void OnShowDeletedChanged(bool value) => ApplyFilter();
    partial void OnShowModifiedChanged(bool value) => ApplyFilter();
    partial void OnShowUnchangedChanged(bool value) => ApplyFilter();

    public void LoadSession(CompareSession session)
    {
        _currentSession = session;
        _allResults = session.Results;

        TotalCount = session.TotalObjects;
        AddedCount = session.AddedCount;
        DeletedCount = session.DeletedCount;
        ModifiedCount = session.ModifiedCount;
        UnchangedCount = session.UnchangedCount;
        HasResults = session.TotalObjects > 0;

        SessionInfo = $"{session.Source.DisplayName}  →  {session.Target.DisplayName}" +
                      $"  |  {session.CompareType}";

        ExecutionTime = $"Completed in {session.ExecutionTime.TotalSeconds:F2}s  " +
                        $"on {session.CompletedAt:yyyy-MM-dd HH:mm:ss}";

        ApplyFilter();

        Log.Information("[Results] Session loaded. Total={Total}", TotalCount);
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_currentSession != null)
            LoadSession(_currentSession);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        ShowAdded = true;
        ShowDeleted = true;
        ShowModified = true;
        ShowUnchanged = false;
    }

    [RelayCommand]
    private void ExportResults()
    {
        if (_currentSession == null)
        {
            _dialogService.ShowInfo("No Results", "No comparison results to export.");
            return;
        }

        ExportRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CopySelectedRow()
    {
        if (SelectedResult == null) return;

        var text = $"{SelectedResult.ObjectType}\t" +
                   $"{SelectedResult.SchemaName}\t" +
                   $"{SelectedResult.ObjectName}\t" +
                   $"{SelectedResult.ChangeType}\t" +
                   $"{SelectedResult.Status}";

        System.Windows.Clipboard.SetText(text);
        Log.Information("[Results] Row copied to clipboard");
    }

    private void ApplyFilter()
    {
        IsLoading = true;

        try
        {
            var filtered = _allResults.AsEnumerable();

            // Change type filters
            filtered = filtered.Where(r =>
                (ShowAdded && r.ChangeType == ChangeType.Added) ||
                (ShowDeleted && r.ChangeType == ChangeType.Deleted) ||
                (ShowModified && r.ChangeType == ChangeType.Modified) ||
                (ShowUnchanged && r.ChangeType == ChangeType.Unchanged));

            // Search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                filtered = filtered.Where(r =>
                    r.ObjectName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.SchemaName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.ObjectType.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    r.ChangeType.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.SourceValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.TargetValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var list = filtered.ToList();
            Results = new ObservableCollection<CompareResult>(list);
            FilteredCount = list.Count;

            // Set up CollectionView for sorting
            ResultsView = CollectionViewSource.GetDefaultView(Results);
            ResultsView.SortDescriptions.Add(
                new SortDescription(nameof(CompareResult.ChangeType),
                    ListSortDirection.Ascending));
            ResultsView.SortDescriptions.Add(
                new SortDescription(nameof(CompareResult.ObjectType),
                    ListSortDirection.Ascending));
            ResultsView.SortDescriptions.Add(
                new SortDescription(nameof(CompareResult.ObjectName),
                    ListSortDirection.Ascending));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Results] Filter failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public CompareSession? GetCurrentSession() => _currentSession;
}