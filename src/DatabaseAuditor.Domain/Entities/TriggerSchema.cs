namespace DatabaseAuditor.Domain.Entities;

using DatabaseAuditor.Domain.Enums;

public class TriggerSchema : SchemaObject
{
    public TriggerSchema()
    {
        ObjectType = CompareType.Trigger;
    }

    public string TableName { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public string ActionTiming { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsSystemObject { get; set; }
    public string? NormalizedDefinition { get; set; }
}