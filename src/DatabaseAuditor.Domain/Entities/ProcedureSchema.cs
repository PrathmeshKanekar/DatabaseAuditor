namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class ProcedureSchema : SchemaObject
{
    public ProcedureSchema()
    {
        ObjectType = CompareType.Procedure;
    }

    public string? ReturnType { get; set; }
    public bool IsSystemObject { get; set; }
    public string? NormalizedDefinition { get; set; }
}