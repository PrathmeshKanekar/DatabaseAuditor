namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Application.UseCases.ManageConnections;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.WPF.Converters;
using DatabaseAuditor.WPF.Helpers;
using DatabaseAuditor.WPF.ViewModels.Dialogs;
using DatabaseAuditor.WPF.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

public partial class ConnectionsViewModel : ObservableObject
{
    private readonly ConnectionService _connectionService;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private ConnectionProfile? _selectedConnection;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _filterEnvironment = "All";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isTesting;

    public ObservableCollection<ConnectionProfile> Connections { get; } = [];
    public ObservableCollection<ConnectionProfile> FilteredConnections { get; } = [];

    public List<string> EnvironmentFilters { get; } =
        ["All", "Development", "QA", "UAT", "Staging", "Production"];

    public ConnectionsViewModel(
        ConnectionService connectionService,
        IDialogService dialogService)
    {
        _connectionService = connectionService;
        _dialogService = dialogService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterEnvironmentChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _connectionService.GetAllAsync();
            Connections.Clear();
            foreach (var c in all) Connections.Add(c);
            ApplyFilter();
        }
        finally { IsLoading = false; }
    }

    private void ApplyFilter()
    {
        FilteredConnections.Clear();
        var query = Connections.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Server.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.DatabaseName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (FilterEnvironment != "All" &&
            Enum.TryParse<EnvironmentType>(FilterEnvironment, out var env))
            query = query.Where(c => c.Environment == env);

        foreach (var c in query) FilteredConnections.Add(c);
    }

    [RelayCommand]
    public async Task AddConnectionAsync()
    {
        var vm = App.ServiceProvider.GetRequiredService<AddConnectionViewModel>();
        var dialog = new AddConnectionDialog(vm) { Owner = App.Current.MainWindow };
        dialog.InitializeForAdd();

        if (dialog.ShowDialog() == true)
        {
            var cmd = new AddConnectionCommand
            {
                Name = vm.Name,
                Environment = vm.SelectedEnvironment,
                DatabaseType = vm.SelectedDatabaseType,
                Server = vm.Server,
                Port = vm.Port,
                DatabaseName = vm.DatabaseName,
                Username = vm.Username,
                Password = vm.Password
            };

            var result = await _connectionService.AddAsync(cmd);
            if (result.IsValid)
            {
                await LoadAsync();
                StatusMessage = $"Connection '{vm.Name}' added successfully.";
            }
            else
            {
                _dialogService.ShowError("Validation Error",
                    string.Join("\n", result.Errors));
            }
        }
    }

    [RelayCommand]
    public async Task EditConnectionAsync()
    {
        if (SelectedConnection == null) return;

        var vm = App.ServiceProvider.GetRequiredService<AddConnectionViewModel>();
        var dialog = new AddConnectionDialog(vm) { Owner = App.Current.MainWindow };
        dialog.InitializeForEdit(SelectedConnection);

        if (dialog.ShowDialog() == true)
        {
            var cmd = new EditConnectionCommand
            {
                Id = SelectedConnection.Id,
                Name = vm.Name,
                Environment = vm.SelectedEnvironment,
                DatabaseType = vm.SelectedDatabaseType,
                Server = vm.Server,
                Port = vm.Port,
                DatabaseName = vm.DatabaseName,
                Username = vm.Username,
                Password = vm.Password
            };

            var result = await _connectionService.UpdateAsync(cmd);
            if (result.IsValid)
            {
                await LoadAsync();
                StatusMessage = $"Connection '{vm.Name}' updated.";
            }
            else
            {
                _dialogService.ShowError("Validation Error",
                    string.Join("\n", result.Errors));
            }
        }
    }

    [RelayCommand]
    public async Task DeleteConnectionAsync()
    {
        if (SelectedConnection == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Connection",
            $"Are you sure you want to delete '{SelectedConnection.Name}'?\nThis action cannot be undone.");

        if (!confirm) return;

        await _connectionService.DeleteAsync(SelectedConnection.Id);
        await LoadAsync();
        StatusMessage = "Connection deleted.";
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        if (SelectedConnection == null) return;

        IsTesting = true;
        StatusMessage = $"Testing connection to {SelectedConnection.Server}...";

        try
        {
            var ok = await _connectionService.TestConnectionAsync(
                SelectedConnection.Id,
                CancellationToken.None);

            StatusMessage = ok
                ? $"✓ Connection to '{SelectedConnection.Name}' succeeded."
                : $"✗ Connection to '{SelectedConnection.Name}' failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally { IsTesting = false; }
    }

    [RelayCommand]
    public async Task ImportConnectionsAsync()
    {
        var path = _dialogService.ShowOpenFileDialog(
            "Import Connections",
            "JSON Files (*.json)|*.json");

        if (path == null) return;

        try
        {
            var profiles = await _connectionService.ImportAsync(path);
            await LoadAsync();
            StatusMessage = $"Imported {profiles.Count} connection(s).";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Import Failed", ex.Message);
        }
    }

    [RelayCommand]
    public async Task ExportConnectionsAsync()
    {
        var path = _dialogService.ShowSaveFileDialog(
            "Export Connections",
            "JSON Files (*.json)|*.json",
            "connections_export.json");

        if (path == null) return;

        try
        {
            await _connectionService.ExportAsync(path);
            StatusMessage = "Connections exported successfully.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Export Failed", ex.Message);
        }
    }
}