namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class ConstraintSchema : SchemaObject
{
    public ConstraintSchema()
    {
        ObjectType = CompareType.Constraint;
    }

    public string TableName { get; set; } = string.Empty;
    public string ConstraintType { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public string? ReferencedTable { get; set; }
    public string? ReferencedColumn { get; set; }
    public string? CheckClause { get; set; }
    public bool IsSystemNamed { get; set; }
}