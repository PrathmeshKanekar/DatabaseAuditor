namespace DatabaseAuditor.Domain.ValueObjects;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;

public class CompareSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ConnectionProfile Source { get; set; } = null!;
    public ConnectionProfile Target { get; set; } = null!;
    public CompareType CompareType { get; set; }
    public List<CompareResult> Results { get; set; } = [];
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public TimeSpan ExecutionTime => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;

    public int TotalObjects => Results.Count;
    public int AddedCount => Results.Count(r => r.ChangeType == ChangeType.Added);
    public int DeletedCount => Results.Count(r => r.ChangeType == ChangeType.Deleted);
    public int ModifiedCount => Results.Count(r => r.ChangeType == ChangeType.Modified);
    public int UnchangedCount => Results.Count(r => r.ChangeType == ChangeType.Unchanged);
}