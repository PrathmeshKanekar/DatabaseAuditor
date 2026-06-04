namespace DatabaseAuditor.Application.UseCases.ManageConnections;

using DatabaseAuditor.Domain.Enums;

public class EditConnectionCommand
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EnvironmentType Environment { get; set; }
    public DatabaseType DatabaseType { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}