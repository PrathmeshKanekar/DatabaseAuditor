namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Application.Services;
using DatabaseAuditor.Domain.ValueObjects;
using Serilog;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    private int _totalConnections;

    [ObservableProperty]
    private int _totalComparisons;

    [ObservableProperty]
    private int _totalAdded;

    [ObservableProperty]
    private int _totalDeleted;

    [ObservableProperty]
    private int _totalModified;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _lastComparedAt = "Never";

    [ObservableProperty]
    private string _welcomeMessage = $"Welcome back, {Environment.UserName}";

    [ObservableProperty]
    private List<RecentActivity> _recentActivities = [];

    [ObservableProperty]
    private CompareSession? _lastSession;

    public DashboardViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var connections = await _connectionService.GetAllAsync();
            TotalConnections = connections.Count;
            Log.Information("[Dashboard] Loaded. Connections={Count}", TotalConnections);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Dashboard] Failed to load");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
        => await LoadAsync();

    public void ApplySession(CompareSession session)
    {
        LastSession = session;
        TotalAdded = session.AddedCount;
        TotalDeleted = session.DeletedCount;
        TotalModified = session.ModifiedCount;
        TotalComparisons++;
        LastComparedAt = session.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";

        RecentActivities.Insert(0, new RecentActivity
        {
            Description = $"Compared {session.Source.DatabaseName} vs {session.Target.DatabaseName}",
            CompareType = session.CompareType.ToString(),
            ResultSummary = $"+{session.AddedCount} / -{session.DeletedCount} / ~{session.ModifiedCount}",
            OccurredAt = session.CompletedAt ?? DateTime.Now,
            IsSuccess = session.IsSuccess
        });

        if (RecentActivities.Count > 10)
            RecentActivities = RecentActivities.Take(10).ToList();

        OnPropertyChanged(nameof(RecentActivities));
    }
}

public class RecentActivity
{
    public string Description { get; set; } = string.Empty;
    public string CompareType { get; set; } = string.Empty;
    public string ResultSummary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public bool IsSuccess { get; set; }

    public string OccurredAtDisplay =>
        OccurredAt.ToString("yyyy-MM-dd HH:mm");

    public string StatusIcon =>
        IsSuccess ? "✓" : "✕";
}