namespace DatabaseAuditor.Application.Validators;

using DatabaseAuditor.Application.UseCases.ManageConnections;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = [];

    public void AddError(string error) => Errors.Add(error);
}

public class ConnectionProfileValidator
{
    public ValidationResult Validate(AddConnectionCommand command)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(command.Name))
            result.AddError("Connection name is required.");

        if (string.IsNullOrWhiteSpace(command.Server))
            result.AddError("Server is required.");

        if (string.IsNullOrWhiteSpace(command.DatabaseName))
            result.AddError("Database name is required.");

        if (string.IsNullOrWhiteSpace(command.Username))
            result.AddError("Username is required.");

        if (string.IsNullOrWhiteSpace(command.Password))
            result.AddError("Password is required.");

        if (command.Port <= 0 || command.Port > 65535)
            result.AddError("Port must be between 1 and 65535.");

        return result;
    }

    public ValidationResult Validate(EditConnectionCommand command)
    {
        var result = new ValidationResult();

        if (command.Id == Guid.Empty)
            result.AddError("Connection ID is required.");

        if (string.IsNullOrWhiteSpace(command.Name))
            result.AddError("Connection name is required.");

        if (string.IsNullOrWhiteSpace(command.Server))
            result.AddError("Server is required.");

        if (string.IsNullOrWhiteSpace(command.DatabaseName))
            result.AddError("Database name is required.");

        if (string.IsNullOrWhiteSpace(command.Username))
            result.AddError("Username is required.");

        if (string.IsNullOrWhiteSpace(command.Password))
            result.AddError("Password is required.");

        if (command.Port <= 0 || command.Port > 65535)
            result.AddError("Port must be between 1 and 65535.");

        return result;
    }
}