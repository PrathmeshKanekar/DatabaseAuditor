namespace DatabaseAuditor.WPF.ViewModels.Dialogs;

using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Entities;
using System;
using System.Collections.ObjectModel;

public partial class AddConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private EnvironmentType _selectedEnvironment = EnvironmentType.Development;

    [ObservableProperty]
    private DatabaseType _selectedDatabaseType = DatabaseType.SqlServer;

    [ObservableProperty]
    private string _server = string.Empty;

    [ObservableProperty]
    private int _port = 1433;

    [ObservableProperty]
    private string _databaseName = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _dialogTitle = "Add Connection";

    public ObservableCollection<EnvironmentType> Environments { get; } = new(Enum.GetValues<EnvironmentType>());
    public ObservableCollection<DatabaseType> DatabaseTypes { get; } = new(Enum.GetValues<DatabaseType>());

    public void LoadFromProfile(ConnectionProfile profile)
    {
        Name = profile.Name;
        SelectedEnvironment = profile.Environment;
        SelectedDatabaseType = profile.DatabaseType;
        Server = profile.Server;
        Port = profile.Port;
        DatabaseName = profile.DatabaseName;
        Username = profile.Username;
        Password = profile.Password;
        DialogTitle = "Edit Connection";
    }

    partial void OnSelectedDatabaseTypeChanged(DatabaseType value)
    {
        Port = value switch
        {
            DatabaseType.SqlServer => 1433,
            DatabaseType.Oracle => 1521,
            DatabaseType.PostgreSql => 5432,
            DatabaseType.MySql => 3306,
            DatabaseType.MariaDb => 3306,
            _ => 1433
        };
    }
}
