namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;
using System.Collections.Generic;

public class UserDefinedTableTypeSchema : SchemaObject
{
    public UserDefinedTableTypeSchema()
    {
        ObjectType = CompareType.UserDefinedTableType;
    }

    public List<ColumnSchema> Columns { get; set; } = [];
}
