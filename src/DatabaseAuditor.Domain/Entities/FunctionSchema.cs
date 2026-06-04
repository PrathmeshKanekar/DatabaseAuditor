namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class FunctionSchema : SchemaObject
{
    public FunctionSchema()
    {
        ObjectType = CompareType.Function;
    }

    public string FunctionType { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public bool IsSystemObject { get; set; }
    public bool IsDeterministic { get; set; }
    public string? NormalizedDefinition { get; set; }
}