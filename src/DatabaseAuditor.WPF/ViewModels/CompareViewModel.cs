namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Application.UseCases.CompareDatabase;
using DatabaseAuditor.Application.Validators;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.ValueObjects;
using DatabaseAuditor.WPF.Converters;
using DatabaseAuditor.WPF.Helpers;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;

public partial class CompareViewModel : ObservableObject
{
    private readonly ConnectionService _connectionService;
    private readonly DatabaseObjectService _databaseObjectService;
    private readonly CompareDatabaseHandler _compareHandler;
    private readonly CompareRequestValidator _validator;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _loadObjectsCts;

    [ObservableProperty]
    private ObservableCollection<ConnectionProfile> _connections = [];

    [ObservableProperty]
    private ConnectionProfile? _selectedSource;

    [ObservableProperty]
    private ConnectionProfile? _selectedTarget;

    [ObservableProperty]
    private ObservableCollection<EnumDisplayItem> _comparisonScopes = [];

    [ObservableProperty]
    private EnumDisplayItem? _selectedComparisonScope;

    [ObservableProperty]
    private ObservableCollection<EnumDisplayItem> _objectTypes = [];

    [ObservableProperty]
    private EnumDisplayItem? _selectedObjectType;

    [ObservableProperty]
    private ObservableCollection<SelectableObjectItem> _availableObjects = [];

    [ObservableProperty]
    private ObservableCollection<SelectableObjectItem> _filteredObjects = [];

    [ObservableProperty]
    private SelectableObjectItem? _selectedObjectItem;

    [ObservableProperty]
    private string _objectSearchText = string.Empty;

    [ObservableProperty]
    private bool _isLoadingObjects;

    [ObservableProperty]
    private bool _isComparing;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _includeUnchanged;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private string _progressMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private CompareSession? _lastSession;

    [ObservableProperty]
    private int _selectedObjectsCount;

    public event EventHandler<CompareSession>? CompareCompleted;

    public CompareViewModel(
        ConnectionService connectionService,
        DatabaseObjectService databaseObjectService,
        CompareDatabaseHandler compareHandler,
        CompareRequestValidator validator,
        IDialogService dialogService)
    {
        _connectionService = connectionService;
        _databaseObjectService = databaseObjectService;
        _compareHandler = compareHandler;
        _validator = validator;
        _dialogService = dialogService;

        ComparisonScopes = new ObservableCollection<EnumDisplayItem>(EnumHelper.GetComparisonScopes());
        ObjectTypes = new ObservableCollection<EnumDisplayItem>(EnumHelper.GetCompareTypes());
        SelectedComparisonScope = ComparisonScopes.FirstOrDefault();
        SelectedObjectType = ObjectTypes.FirstOrDefault();
    }

    public bool IsEntireDatabaseScope => CurrentScope == ComparisonScope.EntireDatabase;
    public bool IsObjectSelectionVisible => CurrentScope != ComparisonScope.EntireDatabase;
    public bool IsSingleObjectMode => CurrentScope == ComparisonScope.SingleObject;
    public bool IsMultipleObjectMode => CurrentScope == ComparisonScope.MultipleObjects;
    public bool HasAvailableObjects => FilteredObjects.Count > 0;
    public string SelectedObjectsSummary => $"{SelectedObjectsCount} selected";

    private ComparisonScope CurrentScope =>
        SelectedComparisonScope?.Value is ComparisonScope scope
            ? scope
            : ComparisonScope.EntireDatabase;

    private CompareType CurrentObjectType =>
        SelectedObjectType?.Value is CompareType compareType
            ? compareType
            : CompareType.Table;

    partial void OnSelectedSourceChanged(ConnectionProfile? value) => _ = ReloadObjectsAsync();
    partial void OnSelectedTargetChanged(ConnectionProfile? value) => _ = ReloadObjectsAsync();
    partial void OnSelectedObjectTypeChanged(EnumDisplayItem? value) => _ = ReloadObjectsAsync();

    partial void OnSelectedComparisonScopeChanged(EnumDisplayItem? value)
    {
        OnPropertyChanged(nameof(IsEntireDatabaseScope));
        OnPropertyChanged(nameof(IsObjectSelectionVisible));
        OnPropertyChanged(nameof(IsSingleObjectMode));
        OnPropertyChanged(nameof(IsMultipleObjectMode));

        if (CurrentScope == ComparisonScope.EntireDatabase)
        {
            SelectedObjectItem = null;
            SetAllSelections(false);
        }

        _ = ReloadObjectsAsync();
    }

    partial void OnObjectSearchTextChanged(string value) => ApplyObjectFilter();

    partial void OnSelectedObjectItemChanged(SelectableObjectItem? value)
    {
        if (!IsSingleObjectMode || value == null)
            return;

        foreach (var item in AvailableObjects)
            item.IsSelected = ReferenceEquals(item, value);

        UpdateSelectedObjectsCount();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var connections = await _connectionService.GetAllAsync();
            Connections = new ObservableCollection<ConnectionProfile>(connections);

            if (Connections.Count >= 2)
            {
                SelectedSource = Connections[0];
                SelectedTarget = Connections[1];
            }

            Log.Information("[Compare] Loaded {Count} connections", Connections.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load connections: {ex.Message}";
            Log.Error(ex, "[Compare] Load failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
        => await LoadAsync();

    [RelayCommand]
    private async Task RefreshObjectsAsync()
        => await ReloadObjectsAsync(forceRefresh: true);

    [RelayCommand]
    private async Task CompareAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        var command = BuildCommand();
        if (command == null) return;

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            HasError = true;
            ErrorMessage = string.Join(Environment.NewLine, validation.Errors);
            return;
        }

        IsComparing = true;
        IsIndeterminate = true;
        ProgressValue = 0;
        ProgressMessage = "Initializing comparison...";

        _cts = new CancellationTokenSource();

        try
        {
            ProgressMessage = $"Comparing {SelectedSource!.DatabaseName} -> {SelectedTarget!.DatabaseName}...";

            var session = await _compareHandler.HandleAsync(command, _cts.Token);

            LastSession = session;
            ProgressValue = 100;

            Log.Information("[CompareViewModel] Received session. Id={SessionId} Results={Count}",
                session.Id, session.Results.Count);

            if (session.IsSuccess)
            {
                StatusMessage = $"Comparison complete — {session.TotalObjects} results generated in {session.ExecutionTime.TotalSeconds:F2}s";
                ProgressMessage = "Complete";
                CompareCompleted?.Invoke(this, session);

                Log.Information("[Compare] Complete. Source={Source} Target={Target} Scope={Scope} Type={Type} Total={Total}",
                    session.Source.DatabaseName,
                    session.Target.DatabaseName,
                    session.ComparisonScope,
                    session.CompareType,
                    session.TotalObjects);
            }
            else
            {
                HasError = true;
                ErrorMessage = session.ErrorMessage ?? "Unknown error occurred.";
                ProgressMessage = "Failed";
                Log.Error("[Compare] Failed: {Error}", session.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Comparison cancelled.";
            ProgressMessage = "Cancelled";
            Log.Information("[Compare] Cancelled by user");
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            ProgressMessage = "Error";
            Log.Error(ex, "[Compare] Unexpected error");
        }
        finally
        {
            IsComparing = false;
            IsIndeterminate = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelCompare()
    {
        _cts?.Cancel();
        ProgressMessage = "Cancelling...";
        Log.Information("[Compare] Cancel requested");
    }

    [RelayCommand]
    private void ClearCompare()
    {
        SelectedSource = null;
        SelectedTarget = null;
        SelectedComparisonScope = ComparisonScopes.FirstOrDefault();
        SelectedObjectType = ObjectTypes.FirstOrDefault();
        SelectedObjectItem = null;
        AvailableObjects.Clear();
        FilteredObjects.Clear();
        ObjectSearchText = string.Empty;
        IncludeUnchanged = false;
        ProgressValue = 0;
        ProgressMessage = string.Empty;
        StatusMessage = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
        LastSession = null;
        SelectedObjectsCount = 0;
    }

    [RelayCommand]
    private void SwapConnections()
    {
        (SelectedSource, SelectedTarget) = (SelectedTarget, SelectedSource);
    }

    [RelayCommand]
    private void SelectAllObjects()
    {
        foreach (var item in AvailableObjects)
            item.IsSelected = true;
        UpdateSelectedObjectsCount();
    }

    [RelayCommand]
    private void DeselectAllObjects()
    {
        SetAllSelections(false);
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var item in AvailableObjects)
            item.IsSelected = !item.IsSelected;
        UpdateSelectedObjectsCount();
    }

    private async Task ReloadObjectsAsync(bool forceRefresh = false)
    {
        if (CurrentScope == ComparisonScope.EntireDatabase)
        {
            AvailableObjects.Clear();
            FilteredObjects.Clear();
            SelectedObjectItem = null;
            SelectedObjectsCount = 0;
            OnPropertyChanged(nameof(HasAvailableObjects));
            OnPropertyChanged(nameof(SelectedObjectsSummary));
            return;
        }

        if (SelectedSource == null || SelectedTarget == null || SelectedObjectType == null)
            return;

        _loadObjectsCts?.Cancel();
        _loadObjectsCts?.Dispose();
        _loadObjectsCts = new CancellationTokenSource();

        IsLoadingObjects = true;
        ProgressMessage = "Loading database objects...";

        try
        {
            var objectNames = await _databaseObjectService.GetObjectNamesAsync(
                SelectedSource.Id,
                SelectedTarget.Id,
                CurrentObjectType,
                _loadObjectsCts.Token);

            AvailableObjects = new ObservableCollection<SelectableObjectItem>(
                objectNames.Select(name =>
                {
                    var item = new SelectableObjectItem(name);
                    item.PropertyChanged += OnSelectableObjectPropertyChanged;
                    return item;
                }));

            ApplyObjectFilter();

            if (IsSingleObjectMode)
            {
                SelectedObjectItem = FilteredObjects.FirstOrDefault();
                if (SelectedObjectItem != null)
                    SelectedObjectItem.IsSelected = true;
            }

            UpdateSelectedObjectsCount();
            Log.Information("[Compare] Loaded {Count} selectable objects for {Type}",
                AvailableObjects.Count, CurrentObjectType);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Failed to load objects: {ex.Message}";
            Log.Error(ex, "[Compare] Object load failed");
        }
        finally
        {
            IsLoadingObjects = false;
            ProgressMessage = string.Empty;
        }
    }

    private void ApplyObjectFilter()
    {
        IEnumerable<SelectableObjectItem> items = AvailableObjects;

        if (!string.IsNullOrWhiteSpace(ObjectSearchText))
        {
            items = items.Where(o =>
                o.Name.Contains(ObjectSearchText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        FilteredObjects = new ObservableCollection<SelectableObjectItem>(
            items.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(HasAvailableObjects));
    }

    private void OnSelectableObjectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableObjectItem.IsSelected) || sender is not SelectableObjectItem item)
            return;

        if (IsSingleObjectMode && item.IsSelected)
        {
            foreach (var other in AvailableObjects.Where(o => !ReferenceEquals(o, item) && o.IsSelected))
                other.IsSelected = false;

            SelectedObjectItem = item;
        }

        UpdateSelectedObjectsCount();
    }

    private void SetAllSelections(bool isSelected)
    {
        foreach (var item in AvailableObjects)
            item.IsSelected = isSelected;

        if (!isSelected)
            SelectedObjectItem = null;

        UpdateSelectedObjectsCount();
    }

    private void UpdateSelectedObjectsCount()
    {
        SelectedObjectsCount = AvailableObjects.Count(o => o.IsSelected);
        OnPropertyChanged(nameof(SelectedObjectsSummary));
    }

    private CompareDatabaseCommand? BuildCommand()
    {
        if (SelectedSource == null)
        {
            HasError = true;
            ErrorMessage = "Please select a source connection.";
            return null;
        }

        if (SelectedTarget == null)
        {
            HasError = true;
            ErrorMessage = "Please select a target connection.";
            return null;
        }

        var scope = CurrentScope;
        var compareType = scope == ComparisonScope.EntireDatabase
            ? CompareType.Database
            : CurrentObjectType;

        var selectedObjects = scope switch
        {
            ComparisonScope.SingleObject when SelectedObjectItem != null => [SelectedObjectItem.Name],
            ComparisonScope.MultipleObjects => AvailableObjects
                .Where(o => o.IsSelected)
                .Select(o => o.Name)
                .ToList(),
            _ => []
        };

        return new CompareDatabaseCommand
        {
            SourceConnectionId = SelectedSource.Id,
            TargetConnectionId = SelectedTarget.Id,
            CompareType = compareType,
            ComparisonScope = scope,
            SelectedObjects = selectedObjects,
            IncludeUnchanged = IncludeUnchanged
        };
    }
}

public partial class SelectableObjectItem : ObservableObject
{
    public SelectableObjectItem(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}
