namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class ColumnSchema : SchemaObject
{
    public ColumnSchema()
    {
        ObjectType = CompareType.Column;
    }

    public string TableName { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
    public string DataType { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsComputed { get; set; }
    public string? DefaultValue { get; set; }
    public string? CollationName { get; set; }

    public string DataTypeFull => MaxLength.HasValue
        ? $"{DataType}({MaxLength})"
        : Precision.HasValue
            ? $"{DataType}({Precision},{Scale ?? 0})"
            : DataType;
}