namespace DatabaseAuditor.Domain.Enums;

public enum ObjectStatus
{
    Match = 1,
    Mismatch = 2,
    MissingInSource = 3,
    MissingInTarget = 4
}