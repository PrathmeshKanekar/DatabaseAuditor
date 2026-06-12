namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Application.UseCases.CompareDatabase;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.WPF.Converters;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

public class SelectableObject : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public string Name { get; init; } = string.Empty;
}

public partial class CompareViewModel : ObservableObject
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly CompareDatabaseHandler _compareHandler;
    private readonly DatabaseObjectService _objectService;
    private CancellationTokenSource? _loadObjectsCts;

    [ObservableProperty] private bool _isComparing;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ConnectionProfile? _selectedSource;
    [ObservableProperty] private ConnectionProfile? _selectedTarget;
    [ObservableProperty] private EnumDisplayItem? _selectedCompareType;
    [ObservableProperty] private EnumDisplayItem? _selectedComparisonScope;
    [ObservableProperty] private bool _includeUnchanged;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCompareCommand))]
    private bool _canCompare;

    [ObservableProperty] private bool _isObjectSelectionVisible;
    [ObservableProperty] private bool _isSingleObjectScope;
    [ObservableProperty] private bool _isMultipleObjectsScope;
    [ObservableProperty] private bool _isLoadingObjects;
    [ObservableProperty] private string _objectSearchText = string.Empty;
    [ObservableProperty] private SelectableObject? _selectedSingleObject;

    public ObservableCollection<ConnectionProfile> Connections { get; } = [];
    public List<EnumDisplayItem> CompareTypes { get; } = EnumHelper.GetCompareTypes();
    public List<EnumDisplayItem> ComparisonScopes { get; } = EnumHelper.GetComparisonScopes();
    public ObservableCollection<SelectableObject> AvailableObjects { get; } = [];
    public ObservableCollection<SelectableObject> FilteredAvailableObjects { get; } = [];

    // Set by MainWindow after compare — used by ResultsViewModel
    public static Domain.ValueObjects.CompareSession? LastSession { get; private set; }

    // Callback to navigate to results
    public Action? OnCompareCompleted { get; set; }

    public CompareViewModel(
        IConnectionRepository connectionRepository,
        CompareDatabaseHandler compareHandler,
        DatabaseObjectService objectService)
    {
        _connectionRepository = connectionRepository;
        _compareHandler = compareHandler;
        _objectService = objectService;
        _selectedCompareType = CompareTypes[0]; // Tables
        _selectedComparisonScope = ComparisonScopes[0]; // Entire Database
    }

    partial void OnSelectedSourceChanged(ConnectionProfile? value)
    {
        UpdateCanCompare();
        _ = LoadObjectsAsync();
    }

    partial void OnSelectedTargetChanged(ConnectionProfile? value)
    {
        UpdateCanCompare();
        _ = LoadObjectsAsync();
    }

    public bool IsScopeSelectionEnabled => SelectedCompareType?.Value is not CompareType ct || ct != CompareType.Database;

    partial void OnSelectedCompareTypeChanged(EnumDisplayItem? value)
    {
        if (value?.Value is CompareType ct && ct == CompareType.Database)
        {
            SelectedComparisonScope = ComparisonScopes.FirstOrDefault(s => (ComparisonScope)s.Value == ComparisonScope.EntireDatabase);
        }
        OnPropertyChanged(nameof(IsScopeSelectionEnabled));
        UpdateCanCompare();
        _ = LoadObjectsAsync();
    }

    partial void OnSelectedComparisonScopeChanged(EnumDisplayItem? value)
    {
        if (value?.Value is ComparisonScope scope)
        {
            IsObjectSelectionVisible = scope != ComparisonScope.EntireDatabase;
            IsSingleObjectScope = scope == ComparisonScope.SingleObject;
            IsMultipleObjectsScope = scope == ComparisonScope.MultipleObjects;
        }
        else
        {
            IsObjectSelectionVisible = false;
            IsSingleObjectScope = false;
            IsMultipleObjectsScope = false;
        }

        UpdateCanCompare();
        _ = LoadObjectsAsync();
    }

    partial void OnObjectSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedSingleObjectChanged(SelectableObject? value)
    {
        OnPropertyChanged(nameof(SelectedCountText));
        UpdateCanCompare();
    }

    public string SelectedCountText
    {
        get
        {
            if (SelectedComparisonScope?.Value is ComparisonScope scope)
            {
                if (scope == ComparisonScope.SingleObject)
                {
                    return SelectedSingleObject != null ? "1 object selected" : "No object selected";
                }
                if (scope == ComparisonScope.MultipleObjects)
                {
                    var count = AvailableObjects.Count(o => o.IsSelected);
                    return $"{count} object(s) selected";
                }
            }
            return string.Empty;
        }
    }

    private void ApplyFilter()
    {
        FilteredAvailableObjects.Clear();
        var search = ObjectSearchText?.Trim();
        foreach (var obj in AvailableObjects)
        {
            if (string.IsNullOrEmpty(search) || obj.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableObjects.Add(obj);
            }
        }
    }

    private async Task LoadObjectsAsync()
    {
        _loadObjectsCts?.Cancel();
        _loadObjectsCts = new CancellationTokenSource();
        var token = _loadObjectsCts.Token;

        if (SelectedSource == null || SelectedTarget == null || SelectedComparisonScope?.Value is ComparisonScope scope && scope == ComparisonScope.EntireDatabase)
        {
            AvailableObjects.Clear();
            FilteredAvailableObjects.Clear();
            OnPropertyChanged(nameof(SelectedCountText));
            UpdateCanCompare();
            return;
        }

        IsLoadingObjects = true;
        try
        {
            var compareType = SelectedCompareType?.Value is CompareType ct ? ct : CompareType.Table;
            var names = await _objectService.GetObjectNamesAsync(SelectedSource.Id, SelectedTarget.Id, compareType, token);

            if (token.IsCancellationRequested) return;

            AvailableObjects.Clear();
            foreach (var name in names)
            {
                var obj = new SelectableObject { Name = name };
                obj.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableObject.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCountText));
                        UpdateCanCompare();
                    }
                };
                AvailableObjects.Add(obj);
            }
            SelectedSingleObject = null;
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading database objects: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingObjects = false;
                OnPropertyChanged(nameof(SelectedCountText));
                UpdateCanCompare();
            }
        }
    }

    private void UpdateCanCompare()
    {
        if (SelectedSource == null || SelectedTarget == null || SelectedSource.Id == SelectedTarget.Id)
        {
            CanCompare = false;
            return;
        }

        if (SelectedComparisonScope?.Value is ComparisonScope scope)
        {
            if (scope == ComparisonScope.SingleObject)
            {
                CanCompare = SelectedSingleObject != null;
                return;
            }
            if (scope == ComparisonScope.MultipleObjects)
            {
                CanCompare = AvailableObjects.Any(o => o.IsSelected);
                return;
            }
        }

        CanCompare = true;
    }

    [RelayCommand]
    public async Task LoadConnectionsAsync()
    {
        var all = await _connectionRepository.GetAllAsync();
        Connections.Clear();
        foreach (var c in all) Connections.Add(c);
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    public async Task RunCompareAsync(CancellationToken cancellationToken)
    {
        if (SelectedSource == null || SelectedTarget == null) return;

        IsComparing = true;
        StatusMessage = "Initialising comparison…";
        ProgressValue = 0;

        try
        {
            var compareType = SelectedCompareType?.Value is CompareType ct
                ? ct
                : CompareType.Database;

            StatusMessage = $"Comparing {compareType} objects between '{SelectedSource.Name}' and '{SelectedTarget.Name}'…";
            ProgressValue = 20;

            var scope = SelectedComparisonScope?.Value is ComparisonScope cs ? cs : ComparisonScope.EntireDatabase;
            var selectedObjects = new List<string>();

            if (scope == ComparisonScope.SingleObject && SelectedSingleObject != null)
            {
                selectedObjects.Add(SelectedSingleObject.Name);
            }
            else if (scope == ComparisonScope.MultipleObjects)
            {
                selectedObjects.AddRange(AvailableObjects.Where(o => o.IsSelected).Select(o => o.Name));
            }

            var command = new CompareDatabaseCommand
            {
                SourceConnectionId = SelectedSource.Id,
                TargetConnectionId = SelectedTarget.Id,
                CompareType = compareType,
                ComparisonScope = scope,
                SelectedObjects = selectedObjects,
                IncludeUnchanged = IncludeUnchanged
            };

            ProgressValue = 40;

            var session = await _compareHandler.HandleAsync(command, cancellationToken);

            ProgressValue = 90;
            LastSession = session;

            StatusMessage = session.IsSuccess
                ? $"Complete — {session.TotalObjects} objects compared in {session.ExecutionTime.TotalSeconds:F1}s"
                : $"Failed: {session.ErrorMessage}";

            ProgressValue = 100;

            await Task.Delay(300, cancellationToken);
            OnCompareCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Comparison cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsComparing = false;
        }
    }
}