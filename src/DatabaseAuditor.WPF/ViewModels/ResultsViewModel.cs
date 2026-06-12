using System.Linq;
using DatabaseAuditor.Application.UseCases.SyncColumns;

namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.ValueObjects;
using System.Collections.ObjectModel;

public partial class ResultsViewModel : ObservableObject
{
    private readonly SyncColumnsHandler _syncHandler;

    public ResultsViewModel(SyncColumnsHandler syncHandler)
    {
        _syncHandler = syncHandler;
    }
    [ObservableProperty] private int _totalObjects;
    [ObservableProperty] private int _addedCount;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private int _modifiedCount;
    [ObservableProperty] private int _unchangedCount;

    [ObservableProperty] private string _sourceLabel = string.Empty;
    [ObservableProperty] private string _targetLabel = string.Empty;
    [ObservableProperty] private string _compareTypeLabel = string.Empty;
    [ObservableProperty] private string _executionTime = string.Empty;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasResults;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _filterChangeType = "All";
    [ObservableProperty] private string _filterObjectType = "All";

    [ObservableProperty] private CompareResult? _selectedResult;
    [ObservableProperty] private bool _showDiffPanel;

    public ObservableCollection<CompareResult> Results { get; } = [];
    public ObservableCollection<CompareResult> FilteredResults { get; } = [];
    public ObservableCollection<DiffLine> DiffLines { get; } = [];

    public List<string> ChangeTypeFilters { get; } =
        ["All", "Added", "Deleted", "Modified", "Unchanged"];

    public List<string> ObjectTypeFilters { get; } =
        ["All", "Table", "Column", "Procedure", "View", "Function", "Trigger", "Constraint", "Index"];

    public static List<CompareResult>? LastFilteredResults { get; private set; }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnFilterChangeTypeChanged(string value) => ApplyFilter();
    partial void OnFilterObjectTypeChanged(string value) => ApplyFilter();

    partial void OnSelectedResultChanged(CompareResult? value)
    {
        if (value == null)
        {
            ShowDiffPanel = false;
            return;
        }

        ShowDiffPanel = value.ChangeType == ChangeType.Modified || value.Differences.Count > 0;
        BuildDiffView(value);
    }

    public Task LoadFromSessionAsync()
    {
        var session = CompareViewModel.LastSession;
        if (session == null) return Task.CompletedTask;

        HasResults = true;
        SourceLabel = $"[{session.Source.Environment}] {session.Source.Name}";
        TargetLabel = $"[{session.Target.Environment}] {session.Target.Name}";
        CompareTypeLabel = session.CompareType.ToString();
        ExecutionTime = $"{session.ExecutionTime.TotalSeconds:F2}s";
        IsSuccess = session.IsSuccess;
        ErrorMessage = session.ErrorMessage ?? string.Empty;

        TotalObjects = session.TotalObjects;
        AddedCount = session.AddedCount;
        DeletedCount = session.DeletedCount;
        ModifiedCount = session.ModifiedCount;
        UnchangedCount = session.UnchangedCount;

        Results.Clear();
        foreach (var r in session.Results) Results.Add(r);

        ApplyFilter();
        OnPropertyChanged(nameof(HasSyncableResults));
        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        FilteredResults.Clear();
        var query = Results.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            var search = FilterText.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.ObjectName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.SchemaName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (FilterChangeType != "All" && Enum.TryParse<ChangeType>(FilterChangeType, out var ct))
            query = query.Where(r => r.ChangeType == ct);

        if (FilterObjectType != "All" && Enum.TryParse<CompareType>(FilterObjectType, out var ot))
            query = query.Where(r => r.ObjectType == ot);

        // Sort: Modified > Added > Deleted > Unchanged
        query = query.OrderBy(r => r.ChangeType switch
        {
            ChangeType.Modified => 0,
            ChangeType.Added => 1,
            ChangeType.Deleted => 2,
            _ => 3
        }).ThenBy(r => r.ObjectType)
          .ThenBy(r => r.FullObjectName);

        var list = query.ToList();
        foreach (var r in list) FilteredResults.Add(r);
        LastFilteredResults = list;
    }

    private void BuildDiffView(CompareResult result)
    {
        DiffLines.Clear();

        if (result.ChangeType == ChangeType.Added)
        {
            DiffLines.Add(new DiffLine
            {
                Type = DiffLineType.Added,
                SourceLine = result.SourceValue ?? result.ObjectName,
                TargetLine = "Missing",
                LineNumber = 1
            });
            return;
        }

        if (result.ChangeType == ChangeType.Deleted)
        {
            DiffLines.Add(new DiffLine
            {
                Type = DiffLineType.Removed,
                SourceLine = "Missing",
                TargetLine = result.TargetValue ?? result.ObjectName,
                LineNumber = 1
            });
            return;
        }

        // Modified — show each difference as a diff line
        int lineNo = 1;
        foreach (var diff in result.Differences)
        {
            // Check for missing parameter message, e.g. "Parameter '@myParam' is missing in Target on line 15"
            // or "Parameter '@myParam' is missing in Source on line 15"
            if (diff.Contains("is missing in Target"))
            {
                string paramName = string.Empty;
                string lineInfo = string.Empty;

                var match = System.Text.RegularExpressions.Regex.Match(diff, @"Parameter '([^']+)' is missing in Target(?: on line (\d+))?");
                if (match.Success)
                {
                    paramName = match.Groups[1].Value;
                    if (match.Groups[2].Success)
                    {
                        lineInfo = $" (line {match.Groups[2].Value})";
                    }
                }
                else
                {
                    paramName = diff;
                }

                DiffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Removed,
                    SourceLine = $"Parameter '{paramName}'{lineInfo}",
                    TargetLine = "Missing",
                    LineNumber = lineNo++
                });
                continue;
            }
            else if (diff.Contains("is missing in Source"))
            {
                string paramName = string.Empty;
                string lineInfo = string.Empty;

                var match = System.Text.RegularExpressions.Regex.Match(diff, @"Parameter '([^']+)' is missing in Source(?: on line (\d+))?");
                if (match.Success)
                {
                    paramName = match.Groups[1].Value;
                    if (match.Groups[2].Success)
                    {
                        lineInfo = $" (line {match.Groups[2].Value})";
                    }
                }
                else
                {
                    paramName = diff;
                }

                DiffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Added,
                    SourceLine = "Missing",
                    TargetLine = $"Parameter '{paramName}'{lineInfo}",
                    LineNumber = lineNo++
                });
                continue;
            }

            // Parse "Property: OldValue -> NewValue" or "Property: OldValue → NewValue"
            var parts = diff.Contains("→") 
                ? diff.Split('→') 
                : diff.Split(new[] { "->" }, StringSplitOptions.None);

            if (parts.Length == 2)
            {
                var propAndOld = parts[0].Trim();
                var newVal = parts[1].Trim();

                // Split prop from old value at last ':'
                var colonIdx = propAndOld.LastIndexOf(':');
                var prop = colonIdx >= 0 ? propAndOld[..colonIdx].Trim() : propAndOld;
                var oldVal = colonIdx >= 0 ? propAndOld[(colonIdx + 1)..].Trim() : string.Empty;

                DiffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Context,
                    SourceLine = $"-- {prop}",
                    TargetLine = $"-- {prop}",
                    LineNumber = lineNo++
                });
                DiffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Removed,
                    SourceLine = oldVal,
                    TargetLine = string.Empty,
                    LineNumber = lineNo++
                });
                DiffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Added,
                    SourceLine = string.Empty,
                    TargetLine = newVal,
                    LineNumber = lineNo++
                });
            }
            else
            {
                DiffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Modified,
                    SourceLine = diff,
                    TargetLine = diff,
                    LineNumber = lineNo++
                });
            }
        }
    }

    [RelayCommand]
    public void ClearFilter()
    {
        FilterText = string.Empty;
        FilterChangeType = "All";
        FilterObjectType = "All";
    }

    [RelayCommand]
    public void NavigateToReports()
    {
        if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToReports();
        }
    }

    public bool HasSyncableResults => Results.Any(r => !string.IsNullOrEmpty(r.SyncScript));

    private bool _allSelected = false;

    [RelayCommand]
    public void ToggleSelectAll()
    {
        _allSelected = !_allSelected;
        foreach (var r in FilteredResults)
        {
            if (!string.IsNullOrEmpty(r.SyncScript))
            {
                r.IsSelected = _allSelected;
            }
        }
        ApplyFilter();
    }

    [RelayCommand]
    public async Task SyncSelectedAsync()
    {
        var toSync = Results.Where(r => r.IsSelected && !string.IsNullOrEmpty(r.SyncScript)).ToList();
        if (toSync.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Please select one or more columns to synchronize by checking the boxes in the grid.",
                "No Columns Selected",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var session = CompareViewModel.LastSession;
        if (session == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to synchronize the {toSync.Count} selected column(s) to the target database?\nThis will execute ALTER TABLE statements and modify the target database schema.",
            "Confirm Schema Synchronization",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var syncCommand = new SyncColumnsCommand
            {
                TargetConnectionId = session.Target.Id,
                SyncScripts = toSync.Select(s => s.SyncScript!).ToList()
            };

            await _syncHandler.HandleAsync(syncCommand);

            System.Windows.MessageBox.Show(
                "Successfully synchronized the selected columns to the target database!",
                "Sync Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

            // Update local state to reflect that they are now synchronized (Unchanged)
            foreach (var item in toSync)
            {
                item.ChangeType = ChangeType.Unchanged;
                item.Status = ObjectStatus.Match;
                item.SyncScript = null;
                item.IsSelected = false;
            }

            // Recalculate summary metrics
            AddedCount = Results.Count(r => r.ChangeType == ChangeType.Added);
            UnchangedCount = Results.Count(r => r.ChangeType == ChangeType.Unchanged);
            TotalObjects = Results.Count;

            ApplyFilter();
            OnPropertyChanged(nameof(HasSyncableResults));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to synchronize columns: {ex.Message}",
                "Synchronization Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }
}

public enum DiffLineType { Context, Added, Removed, Modified }

public class DiffLine
{
    public int LineNumber { get; set; }
    public DiffLineType Type { get; set; }
    public string SourceLine { get; set; } = string.Empty;
    public string TargetLine { get; set; } = string.Empty;

    public string TypeLabel => Type switch
    {
        DiffLineType.Added => "+",
        DiffLineType.Removed => "-",
        DiffLineType.Modified => "~",
        _ => " "
    };
}