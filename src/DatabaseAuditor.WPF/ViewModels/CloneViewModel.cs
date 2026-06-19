namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class CloneViewModel : ObservableObject
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IDatabaseProviderResolver _providerResolver;
    private CancellationTokenSource? _loadTablesCts;
    private CancellationTokenSource? _loadProceduresCts;
    private CancellationTokenSource? _loadTriggersCts;
    private CancellationTokenSource? _loadTableTypesCts;

    [ObservableProperty] private bool _isCloning;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private ConnectionProfile? _selectedSource;
    [ObservableProperty] private ConnectionProfile? _selectedTarget;
    [ObservableProperty] private bool _isLoadingTables;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloneSelectedTablesCommand))]
    private bool _canClone;

    [ObservableProperty] private bool _isLoadingProcedures;
    [ObservableProperty] private string _searchTextProcedures = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloneSelectedProceduresCommand))]
    private bool _canCloneProcedures;

    [ObservableProperty] private bool _isLoadingTriggers;
    [ObservableProperty] private string _searchTextTriggers = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloneSelectedTriggersCommand))]
    private bool _canCloneTriggers;

    [ObservableProperty] private bool _isLoadingTableTypes;
    [ObservableProperty] private string _searchTextTableTypes = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloneSelectedTableTypesCommand))]
    private bool _canCloneTableTypes;

    public ObservableCollection<ConnectionProfile> Connections { get; } = [];
    public ObservableCollection<SelectableObject> AvailableTables { get; } = [];
    public ObservableCollection<SelectableObject> FilteredAvailableTables { get; } = [];
    public ObservableCollection<SelectableObject> AvailableProcedures { get; } = [];
    public ObservableCollection<SelectableObject> FilteredAvailableProcedures { get; } = [];
    public ObservableCollection<SelectableObject> AvailableTriggers { get; } = [];
    public ObservableCollection<SelectableObject> FilteredAvailableTriggers { get; } = [];
    public ObservableCollection<SelectableObject> AvailableTableTypes { get; } = [];
    public ObservableCollection<SelectableObject> FilteredAvailableTableTypes { get; } = [];

    public CloneViewModel(
        IConnectionRepository connectionRepository,
        IDatabaseProviderResolver providerResolver)
    {
        _connectionRepository = connectionRepository;
        _providerResolver = providerResolver;
    }

    partial void OnSelectedSourceChanged(ConnectionProfile? value)
    {
        UpdateCanClone();
        UpdateCanCloneProcedures();
        UpdateCanCloneTriggers();
        UpdateCanCloneTableTypes();
        _ = LoadSourceTablesAsync();
        _ = LoadSourceProceduresAsync();
        _ = LoadSourceTriggersAsync();
        _ = LoadSourceTableTypesAsync();
    }

    partial void OnSelectedTargetChanged(ConnectionProfile? value)
    {
        UpdateCanClone();
        UpdateCanCloneProcedures();
        UpdateCanCloneTriggers();
        UpdateCanCloneTableTypes();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSearchTextProceduresChanged(string value) => ApplyFilterProcedures();
    partial void OnSearchTextTriggersChanged(string value) => ApplyFilterTriggers();
    partial void OnSearchTextTableTypesChanged(string value) => ApplyFilterTableTypes();

    public string SelectedCountText
    {
        get
        {
            var count = AvailableTables.Count(o => o.IsSelected);
            return $"{count} table(s) selected of {AvailableTables.Count}";
        }
    }

    public string SelectedProceduresCountText
    {
        get
        {
            var count = AvailableProcedures.Count(o => o.IsSelected);
            return $"{count} procedure(s) selected of {AvailableProcedures.Count}";
        }
    }

    public string SelectedTriggersCountText
    {
        get
        {
            var count = AvailableTriggers.Count(o => o.IsSelected);
            return $"{count} trigger(s) selected of {AvailableTriggers.Count}";
        }
    }

    public string SelectedTableTypesCountText
    {
        get
        {
            var count = AvailableTableTypes.Count(o => o.IsSelected);
            return $"{count} table type(s) selected of {AvailableTableTypes.Count}";
        }
    }

    private void ApplyFilter()
    {
        FilteredAvailableTables.Clear();
        var search = SearchText?.Trim();
        foreach (var obj in AvailableTables)
        {
            if (string.IsNullOrEmpty(search) || obj.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableTables.Add(obj);
            }
        }
    }

    private void ApplyFilterProcedures()
    {
        FilteredAvailableProcedures.Clear();
        var search = SearchTextProcedures?.Trim();
        foreach (var obj in AvailableProcedures)
        {
            if (string.IsNullOrEmpty(search) || obj.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableProcedures.Add(obj);
            }
        }
    }

    private void ApplyFilterTriggers()
    {
        FilteredAvailableTriggers.Clear();
        var search = SearchTextTriggers?.Trim();
        foreach (var obj in AvailableTriggers)
        {
            if (string.IsNullOrEmpty(search) || obj.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableTriggers.Add(obj);
            }
        }
    }

    private void ApplyFilterTableTypes()
    {
        FilteredAvailableTableTypes.Clear();
        var search = SearchTextTableTypes?.Trim();
        foreach (var obj in AvailableTableTypes)
        {
            if (string.IsNullOrEmpty(search) || obj.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableTableTypes.Add(obj);
            }
        }
    }

    [RelayCommand]
    public async Task LoadConnectionsAsync()
    {
        var all = await _connectionRepository.GetAllAsync();
        Connections.Clear();
        foreach (var c in all) Connections.Add(c);
    }

    private async Task LoadSourceTablesAsync()
    {
        _loadTablesCts?.Cancel();
        _loadTablesCts = new CancellationTokenSource();
        var token = _loadTablesCts.Token;

        if (SelectedSource == null)
        {
            AvailableTables.Clear();
            FilteredAvailableTables.Clear();
            OnPropertyChanged(nameof(SelectedCountText));
            UpdateCanClone();
            return;
        }

        IsLoadingTables = true;
        StatusMessage = $"Loading tables from '{SelectedSource.Name}'...";

        try
        {
            var provider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var names = await provider.GetTableNamesAsync(SelectedSource, token);

            if (token.IsCancellationRequested) return;

            AvailableTables.Clear();
            foreach (var name in names.OrderBy(n => n))
            {
                var obj = new SelectableObject { Name = name };
                obj.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableObject.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCountText));
                        UpdateCanClone();
                    }
                };
                AvailableTables.Add(obj);
            }

            ApplyFilter();
            StatusMessage = $"Loaded {names.Count} tables.";
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading tables: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingTables = false;
                OnPropertyChanged(nameof(SelectedCountText));
                UpdateCanClone();
            }
        }
    }

    private void UpdateCanClone()
    {
        if (SelectedSource == null || SelectedTarget == null || SelectedSource.Id == SelectedTarget.Id)
        {
            CanClone = false;
            return;
        }

        CanClone = AvailableTables.Any(o => o.IsSelected);
    }

    private bool _allSelected = false;

    [RelayCommand]
    public void ToggleSelectAll()
    {
        _allSelected = !_allSelected;
        foreach (var table in FilteredAvailableTables)
        {
            table.IsSelected = _allSelected;
        }
        OnPropertyChanged(nameof(SelectedCountText));
        UpdateCanClone();
    }

    [RelayCommand(CanExecute = nameof(CanClone))]
    public async Task CloneSelectedTablesAsync(CancellationToken cancellationToken)
    {
        if (SelectedSource == null || SelectedTarget == null) return;

        var toClone = AvailableTables.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        if (toClone.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to clone {toClone.Count} table(s) from '{SelectedSource.Name}' to '{SelectedTarget.Name}'?\nThis will create new tables in the targeted database.",
            "Confirm Table Cloning",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        IsCloning = true;
        ProgressValue = 0;
        StatusMessage = "Starting cloning process...";

        try
        {
            var sourceProvider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var targetProvider = _providerResolver.GetProvider(SelectedTarget.DatabaseType);

            StatusMessage = "Loading full schemas of source tables...";
            ProgressValue = 15;

            // Load full tables schemas including columns, pk, etc.
            var tablesTask = sourceProvider.GetTablesAsync(SelectedSource, toClone, cancellationToken);
            var columnsTask = sourceProvider.GetColumnsAsync(SelectedSource, toClone, cancellationToken);

            await Task.WhenAll(tablesTask, columnsTask);

            var tables = tablesTask.Result;
            var columns = columnsTask.Result;

            var tableMap = tables.ToDictionary(t => t.FullName, t => t, StringComparer.OrdinalIgnoreCase);
            
            foreach (var col in columns)
            {
                var key = string.IsNullOrEmpty(col.SchemaName) ? col.TableName : $"{col.SchemaName}.{col.TableName}";
                if (tableMap.TryGetValue(key, out var t))
                {
                    t.Columns.Add(col);
                }
            }

            int count = 0;
            int total = tables.Count;

            foreach (var table in tables)
            {
                if (cancellationToken.IsCancellationRequested) break;

                StatusMessage = $"Cloning table '{table.FullName}' ({count + 1}/{total})...";

                // Generate CREATE TABLE SQL for the target database type
                var sql = CompareService.GenerateCreateTableSql(table, SelectedTarget.DatabaseType);
                
                // Execute on target
                await targetProvider.ExecuteSqlAsync(SelectedTarget, sql, cancellationToken);

                count++;
                ProgressValue = 15 + (int)((count / (double)total) * 85);
            }

            ProgressValue = 100;
            StatusMessage = $"Successfully cloned {count} table(s) to '{SelectedTarget.Name}'.";

            System.Windows.MessageBox.Show(
                $"Successfully cloned {count} table(s) to '{SelectedTarget.Name}'!",
                "Cloning Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cloning process was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cloning failed: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"An error occurred while cloning tables: {ex.Message}",
                "Cloning Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsCloning = false;
        }
    }

    private bool _allProceduresSelected = false;

    [RelayCommand]
    public void ToggleSelectAllProcedures()
    {
        _allProceduresSelected = !_allProceduresSelected;
        foreach (var proc in FilteredAvailableProcedures)
        {
            proc.IsSelected = _allProceduresSelected;
        }
        OnPropertyChanged(nameof(SelectedProceduresCountText));
        UpdateCanCloneProcedures();
    }

    private void UpdateCanCloneProcedures()
    {
        if (SelectedSource == null || SelectedTarget == null || SelectedSource.Id == SelectedTarget.Id)
        {
            CanCloneProcedures = false;
            return;
        }

        CanCloneProcedures = AvailableProcedures.Any(o => o.IsSelected);
    }

    private async Task LoadSourceProceduresAsync()
    {
        _loadProceduresCts?.Cancel();
        _loadProceduresCts = new CancellationTokenSource();
        var token = _loadProceduresCts.Token;

        if (SelectedSource == null)
        {
            AvailableProcedures.Clear();
            FilteredAvailableProcedures.Clear();
            OnPropertyChanged(nameof(SelectedProceduresCountText));
            UpdateCanCloneProcedures();
            return;
        }

        IsLoadingProcedures = true;
        StatusMessage = $"Loading stored procedures from '{SelectedSource.Name}'...";

        try
        {
            var provider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var names = await provider.GetProcedureNamesAsync(SelectedSource, token);

            if (token.IsCancellationRequested) return;

            AvailableProcedures.Clear();
            foreach (var name in names.OrderBy(n => n))
            {
                var obj = new SelectableObject { Name = name };
                obj.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableObject.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedProceduresCountText));
                        UpdateCanCloneProcedures();
                    }
                };
                AvailableProcedures.Add(obj);
            }

            ApplyFilterProcedures();
            StatusMessage = $"Loaded {names.Count} stored procedures.";
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading stored procedures: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingProcedures = false;
                OnPropertyChanged(nameof(SelectedProceduresCountText));
                UpdateCanCloneProcedures();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCloneProcedures))]
    public async Task CloneSelectedProceduresAsync(CancellationToken cancellationToken)
    {
        if (SelectedSource == null || SelectedTarget == null) return;

        var toClone = AvailableProcedures.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        if (toClone.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to clone {toClone.Count} stored procedure(s) from '{SelectedSource.Name}' to '{SelectedTarget.Name}'?\nThis will create new stored procedures in the targeted database.",
            "Confirm Stored Procedure Cloning",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        IsCloning = true;
        ProgressValue = 0;
        StatusMessage = "Starting stored procedure cloning process...";

        try
        {
            var sourceProvider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var targetProvider = _providerResolver.GetProvider(SelectedTarget.DatabaseType);

            StatusMessage = "Loading definitions of source stored procedures...";
            ProgressValue = 15;

            var procedures = await sourceProvider.GetProceduresAsync(SelectedSource, toClone, cancellationToken);

            int count = 0;
            int total = procedures.Count;

            foreach (var proc in procedures)
            {
                if (cancellationToken.IsCancellationRequested) break;

                StatusMessage = $"Cloning stored procedure '{proc.FullName}' ({count + 1}/{total})...";

                if (string.IsNullOrEmpty(proc.Definition))
                {
                    throw new Exception($"Definition for stored procedure '{proc.FullName}' is empty or cannot be read.");
                }

                // Execute on target
                await targetProvider.ExecuteSqlAsync(SelectedTarget, proc.Definition, cancellationToken);

                count++;
                ProgressValue = 15 + (int)((count / (double)total) * 85);
            }

            ProgressValue = 100;
            StatusMessage = $"Successfully cloned {count} stored procedure(s) to '{SelectedTarget.Name}'.";

            System.Windows.MessageBox.Show(
                $"Successfully cloned {count} stored procedure(s) to '{SelectedTarget.Name}'!",
                "Cloning Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cloning process was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cloning failed: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"An error occurred while cloning stored procedures: {ex.Message}",
                "Cloning Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsCloning = false;
        }
    }

    private bool _allTriggersSelected = false;

    [RelayCommand]
    public void ToggleSelectAllTriggers()
    {
        _allTriggersSelected = !_allTriggersSelected;
        foreach (var trig in FilteredAvailableTriggers)
        {
            trig.IsSelected = _allTriggersSelected;
        }
        OnPropertyChanged(nameof(SelectedTriggersCountText));
        UpdateCanCloneTriggers();
    }

    private void UpdateCanCloneTriggers()
    {
        if (SelectedSource == null || SelectedTarget == null || SelectedSource.Id == SelectedTarget.Id)
        {
            CanCloneTriggers = false;
            return;
        }

        CanCloneTriggers = AvailableTriggers.Any(o => o.IsSelected);
    }

    private async Task LoadSourceTriggersAsync()
    {
        _loadTriggersCts?.Cancel();
        _loadTriggersCts = new CancellationTokenSource();
        var token = _loadTriggersCts.Token;

        if (SelectedSource == null)
        {
            AvailableTriggers.Clear();
            FilteredAvailableTriggers.Clear();
            OnPropertyChanged(nameof(SelectedTriggersCountText));
            UpdateCanCloneTriggers();
            return;
        }

        IsLoadingTriggers = true;
        StatusMessage = $"Loading triggers from '{SelectedSource.Name}'...";

        try
        {
            var provider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var items = await provider.GetTriggersAsync(SelectedSource, null, token);

            if (token.IsCancellationRequested) return;

            AvailableTriggers.Clear();
            foreach (var item in items.OrderBy(n => n.Name))
            {
                var obj = new SelectableObject { Name = item.Name };
                obj.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableObject.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedTriggersCountText));
                        UpdateCanCloneTriggers();
                    }
                };
                AvailableTriggers.Add(obj);
            }

            ApplyFilterTriggers();
            StatusMessage = $"Loaded {items.Count} triggers.";
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading triggers: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingTriggers = false;
                OnPropertyChanged(nameof(SelectedTriggersCountText));
                UpdateCanCloneTriggers();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCloneTriggers))]
    public async Task CloneSelectedTriggersAsync(CancellationToken cancellationToken)
    {
        if (SelectedSource == null || SelectedTarget == null) return;

        var toClone = AvailableTriggers.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        if (toClone.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to clone {toClone.Count} trigger(s) from '{SelectedSource.Name}' to '{SelectedTarget.Name}'?\nThis will create new triggers in the targeted database.",
            "Confirm Trigger Cloning",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        IsCloning = true;
        ProgressValue = 0;
        StatusMessage = "Starting trigger cloning process...";

        try
        {
            var sourceProvider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var targetProvider = _providerResolver.GetProvider(SelectedTarget.DatabaseType);

            StatusMessage = "Loading definitions of source triggers...";
            ProgressValue = 15;

            var triggers = await sourceProvider.GetTriggersAsync(SelectedSource, toClone, cancellationToken);

            int count = 0;
            int total = triggers.Count;

            foreach (var trig in triggers)
            {
                if (cancellationToken.IsCancellationRequested) break;

                StatusMessage = $"Cloning trigger '{trig.FullName}' ({count + 1}/{total})...";

                if (string.IsNullOrEmpty(trig.Definition))
                {
                    throw new Exception($"Definition for trigger '{trig.FullName}' is empty or cannot be read.");
                }

                // Execute on target
                await targetProvider.ExecuteSqlAsync(SelectedTarget, trig.Definition, cancellationToken);

                count++;
                ProgressValue = 15 + (int)((count / (double)total) * 85);
            }

            ProgressValue = 100;
            StatusMessage = $"Successfully cloned {count} trigger(s) to '{SelectedTarget.Name}'.";

            System.Windows.MessageBox.Show(
                $"Successfully cloned {count} trigger(s) to '{SelectedTarget.Name}'!",
                "Cloning Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cloning process was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cloning failed: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"An error occurred while cloning triggers: {ex.Message}",
                "Cloning Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsCloning = false;
        }
    }

    private bool _allTableTypesSelected = false;

    [RelayCommand]
    public void ToggleSelectAllTableTypes()
    {
        _allTableTypesSelected = !_allTableTypesSelected;
        foreach (var tt in FilteredAvailableTableTypes)
        {
            tt.IsSelected = _allTableTypesSelected;
        }
        OnPropertyChanged(nameof(SelectedTableTypesCountText));
        UpdateCanCloneTableTypes();
    }

    private void UpdateCanCloneTableTypes()
    {
        if (SelectedSource == null || SelectedTarget == null || SelectedSource.Id == SelectedTarget.Id)
        {
            CanCloneTableTypes = false;
            return;
        }

        CanCloneTableTypes = AvailableTableTypes.Any(o => o.IsSelected);
    }

    private async Task LoadSourceTableTypesAsync()
    {
        _loadTableTypesCts?.Cancel();
        _loadTableTypesCts = new CancellationTokenSource();
        var token = _loadTableTypesCts.Token;

        if (SelectedSource == null)
        {
            AvailableTableTypes.Clear();
            FilteredAvailableTableTypes.Clear();
            OnPropertyChanged(nameof(SelectedTableTypesCountText));
            UpdateCanCloneTableTypes();
            return;
        }

        IsLoadingTableTypes = true;
        StatusMessage = $"Loading user-defined table types from '{SelectedSource.Name}'...";

        try
        {
            var provider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var names = await provider.GetUserDefinedTableTypeNamesAsync(SelectedSource, token);

            if (token.IsCancellationRequested) return;

            AvailableTableTypes.Clear();
            foreach (var name in names.OrderBy(n => n))
            {
                var obj = new SelectableObject { Name = name };
                obj.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableObject.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedTableTypesCountText));
                        UpdateCanCloneTableTypes();
                    }
                };
                AvailableTableTypes.Add(obj);
            }

            ApplyFilterTableTypes();
            StatusMessage = $"Loaded {names.Count} user-defined table types.";
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading user-defined table types: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsLoadingTableTypes = false;
                OnPropertyChanged(nameof(SelectedTableTypesCountText));
                UpdateCanCloneTableTypes();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCloneTableTypes))]
    public async Task CloneSelectedTableTypesAsync(CancellationToken cancellationToken)
    {
        if (SelectedSource == null || SelectedTarget == null) return;

        var toClone = AvailableTableTypes.Where(t => t.IsSelected).Select(t => t.Name).ToList();
        if (toClone.Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to clone {toClone.Count} user-defined table type(s) from '{SelectedSource.Name}' to '{SelectedTarget.Name}'?\nThis will create new user-defined table types in the targeted database.",
            "Confirm Table Type Cloning",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        IsCloning = true;
        ProgressValue = 0;
        StatusMessage = "Starting table type cloning process...";

        try
        {
            var sourceProvider = _providerResolver.GetProvider(SelectedSource.DatabaseType);
            var targetProvider = _providerResolver.GetProvider(SelectedTarget.DatabaseType);

            StatusMessage = "Loading schemas of source table types...";
            ProgressValue = 15;

            var tableTypes = await sourceProvider.GetUserDefinedTableTypesAsync(SelectedSource, toClone, cancellationToken);

            int count = 0;
            int total = tableTypes.Count;

            foreach (var tt in tableTypes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                StatusMessage = $"Cloning table type '{tt.FullName}' ({count + 1}/{total})...";

                // Generate CREATE TYPE AS TABLE SQL
                var sql = CompareService.GenerateCreateUserDefinedTableTypeSql(tt, SelectedTarget.DatabaseType);

                // Execute on target
                await targetProvider.ExecuteSqlAsync(SelectedTarget, sql, cancellationToken);

                count++;
                ProgressValue = 15 + (int)((count / (double)total) * 85);
            }

            ProgressValue = 100;
            StatusMessage = $"Successfully cloned {count} user-defined table type(s) to '{SelectedTarget.Name}'.";

            System.Windows.MessageBox.Show(
                $"Successfully cloned {count} user-defined table type(s) to '{SelectedTarget.Name}'!",
                "Cloning Complete",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cloning process was cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cloning failed: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"An error occurred while cloning user-defined table types: {ex.Message}",
                "Cloning Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsCloning = false;
        }
    }
}
