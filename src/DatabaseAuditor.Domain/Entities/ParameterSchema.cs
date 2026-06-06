namespace DatabaseAuditor.Domain.Entities;

public class ParameterSchema
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int OrdinalPosition { get; set; }
    public bool IsOutput { get; set; }
}
