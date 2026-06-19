namespace DatabaseAuditor.WPF.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Domain.Interfaces;
using System.Collections.ObjectModel;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IConnectionRepository _connectionRepository;

    [ObservableProperty] private int _totalConnections;
    [ObservableProperty] private int _productionConnections;
    [ObservableProperty] private int _devConnections;
    [ObservableProperty] private int _totalComparisons;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _lastComparedAt = "Never";

    public ObservableCollection<DashboardConnectionItem> RecentConnections { get; } = [];

    public DashboardViewModel(IConnectionRepository connectionRepository)
    {
        _connectionRepository = connectionRepository;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var connections = await _connectionRepository.GetAllAsync();
            TotalConnections = connections.Count;
            ProductionConnections = connections.Count(c =>
                c.Environment == Domain.Enums.EnvironmentType.Production);
            DevConnections = connections.Count(c =>
                c.Environment == Domain.Enums.EnvironmentType.Development);

            RecentConnections.Clear();
            foreach (var conn in connections.TakeLast(5).Reverse())
            {
                RecentConnections.Add(new DashboardConnectionItem
                {
                    Name = conn.Name,
                    Environment = conn.Environment.ToString(),
                    DatabaseType = conn.DatabaseType.ToString(),
                    Server = conn.Server,
                    Database = conn.DatabaseName,
                    CreatedAt = conn.CreatedAt.ToString("yyyy-MM-dd")
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class DashboardConnectionItem
{
    public string Name { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}