namespace DatabaseAuditor.Domain.ValueObjects;

using DatabaseAuditor.Domain.Enums;

public class CompareResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public CompareType ObjectType { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public ObjectStatus Status { get; set; }
    public string? SourceValue { get; set; }
    public string? TargetValue { get; set; }
    public string? ParentObject { get; set; }
    public List<string> Differences { get; set; } = [];
    public string? SyncScript { get; set; }
    public bool IsSelected { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public string FullObjectName => string.IsNullOrEmpty(SchemaName)
        ? ObjectName
        : $"{SchemaName}.{ObjectName}";

    public string AllDifferences => string.Join(System.Environment.NewLine, Differences);
}