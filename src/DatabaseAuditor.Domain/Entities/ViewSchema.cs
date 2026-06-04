namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class ViewSchema : SchemaObject
{
    public ViewSchema()
    {
        ObjectType = CompareType.View;
    }

    public bool IsUpdatable { get; set; }
    public bool IsIndexed { get; set; }
    public string? NormalizedDefinition { get; set; }
}