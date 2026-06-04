namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class IndexSchema : SchemaObject
{
    public IndexSchema()
    {
        ObjectType = CompareType.Index;
    }

    public string TableName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsDisabled { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<string> IncludedColumns { get; set; } = [];
}