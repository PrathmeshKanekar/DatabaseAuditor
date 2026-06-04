namespace DatabaseAuditor.Application.UseCases.CompareDatabase;

using DatabaseAuditor.Domain.Enums;

public class CompareDatabaseCommand
{
    public Guid SourceConnectionId { get; set; }
    public Guid TargetConnectionId { get; set; }
    public CompareType CompareType { get; set; }
    public bool IncludeUnchanged { get; set; } = false;
}