namespace DatabaseAuditor.Application.Validators;

using DatabaseAuditor.Application.UseCases.CompareDatabase;

public class CompareRequestValidator
{
    public ValidationResult Validate(CompareDatabaseCommand command)
    {
        var result = new ValidationResult();

        if (command.SourceConnectionId == Guid.Empty)
            result.AddError("Source connection is required.");

        if (command.TargetConnectionId == Guid.Empty)
            result.AddError("Target connection is required.");

        if (command.SourceConnectionId == command.TargetConnectionId)
            result.AddError("Source and target connections must be different.");

        if (!Enum.IsDefined(typeof(Domain.Enums.CompareType), command.CompareType))
            result.AddError("Invalid compare type selected.");

        return result;
    }
}