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
using DocumentFormat.OpenXml.Spreadsheet;
using Serilog;
using System.Collections.ObjectModel;

public partial class CompareViewModel : ObservableObject
{
    private readonly ConnectionService _connectionService;
    private readonly CompareDatabaseHandler _compareHandler;
    private readonly CompareRequestValidator _validator;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private ObservableCollection<ConnectionProfile> _connections = [];

    [ObservableProperty]
    private ConnectionProfile? _selectedSource;

    [ObservableProperty]
    private ConnectionProfile? _selectedTarget;

    [ObservableProperty]
    private ObservableCollection<EnumDisplayItem> _compareTypes = [];

    [ObservableProperty]
    private EnumDisplayItem? _selectedCompareType;

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

    public event EventHandler<CompareSession>? CompareCompleted;

    public CompareViewModel(
        ConnectionService connectionService,
        CompareDatabaseHandler compareHandler,
        CompareRequestValidator validator,
        IDialogService dialogService)
    {
        _connectionService = connectionService;
        _compareHandler = compareHandler;
        _validator = validator;
        _dialogService = dialogService;

        CompareTypes = new ObservableCollection<EnumDisplayItem>(
            EnumHelper.GetCompareTypes());

        SelectedCompareType = CompareTypes.FirstOrDefault();
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
    private async Task CompareAsync()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        // Validate
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
            ProgressMessage = $"Comparing {SelectedSource!.DatabaseName} → {SelectedTarget!.DatabaseName}...";

            var session = await _compareHandler.HandleAsync(command, _cts.Token);

            LastSession = session;
            ProgressValue = 100;

            if (session.IsSuccess)
            {
                StatusMessage = $"Comparison complete — {session.TotalObjects} objects compared " +
                                  $"in {session.ExecutionTime.TotalSeconds:F2}s";
                ProgressMessage = "Complete";
                CompareCompleted?.Invoke(this, session);

                Log.Information("[Compare] Complete. Source={Source} Target={Target} " +
                    "Total={Total} Added={Added} Deleted={Deleted} Modified={Modified}",
                    session.Source.DatabaseName, session.Target.DatabaseName,
                    session.TotalObjects, session.AddedCount,
                    session.DeletedCount, session.ModifiedCount);
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
        SelectedCompareType = CompareTypes.FirstOrDefault();
        IncludeUnchanged = false;
        ProgressValue = 0;
        ProgressMessage = string.Empty;
        StatusMessage = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
        LastSession = null;
    }

    [RelayCommand]
    private void SwapConnections()
    {
        (SelectedSource, SelectedTarget) = (SelectedTarget, SelectedSource);
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

        if (SelectedCompareType == null)
        {
            HasError = true;
            ErrorMessage = "Please select a compare type.";
            return null;
        }

        return new CompareDatabaseCommand
        {
            SourceConnectionId = SelectedSource.Id,
            TargetConnectionId = SelectedTarget.Id,
            CompareType = (CompareType)SelectedCompareType.Value,
            IncludeUnchanged = IncludeUnchanged
        };
    }

    public bool CanCompare =>
        SelectedSource != null &&
        SelectedTarget != null &&
        SelectedCompareType != null &&
        !IsComparing;
}