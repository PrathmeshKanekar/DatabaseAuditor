namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public abstract class SchemaObject
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string? Definition { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public CompareType ObjectType { get; protected set; }

    public string FullName => string.IsNullOrEmpty(Schema)
        ? Name
        : $"{Schema}.{Name}";
}