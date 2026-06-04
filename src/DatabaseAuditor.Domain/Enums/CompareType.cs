namespace DatabaseAuditor.Domain.Enums;

public enum CompareType
{
    Database = 1,
    Table = 2,
    Column = 3,
    Procedure = 4,
    View = 5,
    Function = 6,
    Trigger = 7,
    Constraint = 8,
    Index = 9
}