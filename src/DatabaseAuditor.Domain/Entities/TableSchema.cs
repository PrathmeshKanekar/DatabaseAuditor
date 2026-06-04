namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class TableSchema : SchemaObject
{
    public TableSchema()
    {
        ObjectType = CompareType.Table;
    }

    public List<ColumnSchema> Columns { get; set; } = [];
    public List<ConstraintSchema> Constraints { get; set; } = [];
    public List<IndexSchema> Indexes { get; set; } = [];
    public long RowCount { get; set; }
    public string? Engine { get; set; }
    public string? Collation { get; set; }
}