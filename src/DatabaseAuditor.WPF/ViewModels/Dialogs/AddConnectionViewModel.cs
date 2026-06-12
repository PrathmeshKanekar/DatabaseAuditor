namespace DatabaseAuditor.WPF.ViewModels.Dialogs;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.WPF.Converters;

public partial class AddConnectionViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private EnvironmentType _selectedEnvironment = EnvironmentType.Development;
    [ObservableProperty] private DatabaseType _selectedDatabaseType = DatabaseType.SqlServer;
    [ObservableProperty] private string _server = string.Empty;
    [ObservableProperty] private int _port = 1433;
    [ObservableProperty] private string _databaseName = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _dialogTitle = "Add Connection";
    [ObservableProperty] private string _validationMessage = string.Empty;

    public List<EnumDisplayItem> EnvironmentTypes { get; } = EnumHelper.GetEnvironmentTypes();
    public List<EnumDisplayItem> DatabaseTypes { get; } = EnumHelper.GetDatabaseTypes();

    // Callback — set by dialog when user clicks Save
    public bool DialogResult { get; set; }

    partial void OnSelectedDatabaseTypeChanged(DatabaseType value)
    {
        // Auto-set default port when database type changes
        Port = EnumHelper.GetDefaultPort(value);
    }

    public void LoadFrom(ConnectionProfile profile)
    {
        IsEditMode = true;
        DialogTitle = "Edit Connection";
        Name = profile.Name;
        SelectedEnvironment = profile.Environment;
        SelectedDatabaseType = profile.DatabaseType;
        Server = profile.Server;
        Port = profile.Port;
        DatabaseName = profile.DatabaseName;
        Username = profile.Username;
        Password = profile.Password;
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        { ValidationMessage = "Connection name is required."; return false; }
        if (string.IsNullOrWhiteSpace(Server))
        { ValidationMessage = "Server is required."; return false; }
        if (string.IsNullOrWhiteSpace(DatabaseName))
        { ValidationMessage = "Database name is required."; return false; }
        if (string.IsNullOrWhiteSpace(Username))
        { ValidationMessage = "Username is required."; return false; }
        if (string.IsNullOrWhiteSpace(Password))
        { ValidationMessage = "Password is required."; return false; }
        if (Port <= 0 || Port > 65535)
        { ValidationMessage = "Port must be between 1 and 65535."; return false; }

        ValidationMessage = string.Empty;
        return true;
    }
}