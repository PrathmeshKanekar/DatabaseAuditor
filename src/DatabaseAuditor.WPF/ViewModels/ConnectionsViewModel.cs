namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Application.UseCases.ManageConnections;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.WPF.Helpers;
using Serilog;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;

public partial class ConnectionsViewModel : ObservableObject
{
    private readonly ConnectionService _connectionService;
    private readonly IDialogService _dialogService;
    private List<ConnectionProfile> _allConnections = [];

    [ObservableProperty]
    private ObservableCollection<ConnectionProfile> _connections = [];

    [ObservableProperty]
    private ConnectionProfile? _selectedConnection;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasConnections;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _filteredCount;

    public ConnectionsViewModel(
        ConnectionService connectionService,
        IDialogService dialogService)
    {
        _connectionService = connectionService;
        _dialogService = dialogService;
    }

    partial void OnSearchTextChanged(string value)
        => ApplyFilter(value);

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            _allConnections = await _connectionService.GetAllAsync();
            ApplyFilter(SearchText);
            TotalCount = _allConnections.Count;
            HasConnections = _allConnections.Count > 0;

            Log.Information("[Connections] Loaded {Count} connections", TotalCount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load connections: {ex.Message}";
            Log.Error(ex, "[Connections] Load failed");
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
    private async Task AddConnectionAsync()
    {
        var dialog = App.ServiceProvider
            .GetRequiredService<Views.Dialogs.AddConnectionDialog>();

        dialog.InitializeForAdd();

        var result = await _dialogService.ShowDialogAsync<ViewModels.Dialogs.AddConnectionViewModel>(dialog);

        if (result == null) return;

        var command = new AddConnectionCommand
        {
            Name = result.Name,
            Environment = result.SelectedEnvironment,
            DatabaseType = result.SelectedDatabaseType,
            Server = result.Server,
            Port = result.Port,
            DatabaseName = result.DatabaseName,
            Username = result.Username,
            Password = result.Password
        };

        var validation = await _connectionService.AddAsync(command);

        if (!validation.IsValid)
        {
            _dialogService.ShowError("Validation Error",
                string.Join(Environment.NewLine, validation.Errors));
            return;
        }

        await LoadAsync();
        StatusMessage = $"Connection '{command.Name}' added successfully.";
        Log.Information("[Connections] Added connection: {Name}", command.Name);
    }

    [RelayCommand]
    private async Task EditConnectionAsync()
    {
        if (SelectedConnection == null) return;

        var dialog = App.ServiceProvider
            .GetRequiredService<Views.Dialogs.AddConnectionDialog>();

        dialog.InitializeForEdit(SelectedConnection);

        var result = await _dialogService.ShowDialogAsync<ViewModels.Dialogs.AddConnectionViewModel>(dialog);

        if (result == null) return;

        var command = new EditConnectionCommand
        {
            Id = SelectedConnection.Id,
            Name = result.Name,
            Environment = result.SelectedEnvironment,
            DatabaseType = result.SelectedDatabaseType,
            Server = result.Server,
            Port = result.Port,
            DatabaseName = result.DatabaseName,
            Username = result.Username,
            Password = result.Password
        };

        var validation = await _connectionService.UpdateAsync(command);

        if (!validation.IsValid)
        {
            _dialogService.ShowError("Validation Error",
                string.Join(Environment.NewLine, validation.Errors));
            return;
        }

        await LoadAsync();
        StatusMessage = $"Connection '{command.Name}' updated successfully.";
        Log.Information("[Connections] Updated connection: {Name}", command.Name);
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection == null) return;

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Delete Connection",
            $"Are you sure you want to delete '{SelectedConnection.Name}'?");

        if (!confirmed) return;

        try
        {
            await _connectionService.DeleteAsync(SelectedConnection.Id);
            await LoadAsync();
            StatusMessage = "Connection deleted successfully.";
            Log.Information("[Connections] Deleted connection: {Name}", SelectedConnection.Name);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Delete Failed", ex.Message);
            Log.Error(ex, "[Connections] Delete failed");
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (SelectedConnection == null) return;

        IsTesting = true;
        StatusMessage = $"Testing connection to '{SelectedConnection.Name}'...";

        try
        {
            var success = await _connectionService.TestConnectionAsync(
                SelectedConnection.Id,
                CancellationToken.None);

            StatusMessage = success
                ? $"✓ Connection to '{SelectedConnection.Name}' successful."
                : $"✕ Connection to '{SelectedConnection.Name}' failed.";

            Log.Information("[Connections] Test result for {Name}: {Success}",
                SelectedConnection.Name, success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"✕ Test failed: {ex.Message}";
            Log.Error(ex, "[Connections] Test connection failed");
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task ImportConnectionsAsync()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "Import Connections",
            "JSON Files (*.json)|*.json");

        if (filePath == null) return;

        try
        {
            var imported = await _connectionService.ImportAsync(filePath);
            await LoadAsync();
            StatusMessage = $"Imported {imported.Count} connection(s) successfully.";
            Log.Information("[Connections] Imported {Count} connections from {Path}",
                imported.Count, filePath);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Import Failed", ex.Message);
            Log.Error(ex, "[Connections] Import failed");
        }
    }

    [RelayCommand]
    private async Task ExportConnectionsAsync()
    {
        var filePath = _dialogService.ShowSaveFileDialog(
            "Export Connections",
            "JSON Files (*.json)|*.json",
            "connections_export.json");

        if (filePath == null) return;

        try
        {
            await _connectionService.ExportAsync(filePath);
            StatusMessage = $"Exported {_allConnections.Count} connection(s) to {filePath}";
            Log.Information("[Connections] Exported to {Path}", filePath);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Export Failed", ex.Message);
            Log.Error(ex, "[Connections] Export failed");
        }
    }

    private void ApplyFilter(string search)
    {
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allConnections
            : _allConnections.Where(c =>
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Server.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.DatabaseName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Environment.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.DatabaseType.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Connections = new ObservableCollection<ConnectionProfile>(filtered);
        FilteredCount = filtered.Count;
        HasConnections = filtered.Count > 0;
    }
}