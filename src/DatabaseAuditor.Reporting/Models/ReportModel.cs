namespace DatabaseAuditor.Reporting.Models;

using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.ValueObjects;

public class ReportModel
{
    public string Title { get; set; } = "Database Audit Report";
    public string CompanyName { get; set; } = "Three Star Infotech";
    public string GeneratedBy { get; set; } = Environment.UserName;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    // Session Info
    public Guid SessionId { get; set; }
    public string SourceEnvironment { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string SourceServer { get; set; } = string.Empty;
    public DatabaseType SourceDatabaseType { get; set; }
    public string TargetEnvironment { get; set; } = string.Empty;
    public string TargetDatabase { get; set; } = string.Empty;
    public string TargetServer { get; set; } = string.Empty;
    public DatabaseType TargetDatabaseType { get; set; }
    public CompareType CompareType { get; set; }

    // Execution
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    // Summary Counts
    public int TotalObjects { get; set; }
    public int AddedCount { get; set; }
    public int DeletedCount { get; set; }
    public int ModifiedCount { get; set; }
    public int UnchangedCount { get; set; }

    // Results grouped by change type
    public List<CompareResult> AddedObjects { get; set; } = [];
    public List<CompareResult> DeletedObjects { get; set; } = [];
    public List<CompareResult> ModifiedObjects { get; set; } = [];
    public List<CompareResult> AllResults { get; set; } = [];

    public static ReportModel FromSession(CompareSession session)
    {
        return new ReportModel
        {
            SessionId = session.Id,
            SourceEnvironment = session.Source.Environment.ToString(),
            SourceDatabase = session.Source.DatabaseName,
            SourceServer = session.Source.Server,
            SourceDatabaseType = session.Source.DatabaseType,
            TargetEnvironment = session.Target.Environment.ToString(),
            TargetDatabase = session.Target.DatabaseName,
            TargetServer = session.Target.Server,
            TargetDatabaseType = session.Target.DatabaseType,
            CompareType = session.CompareType,
            StartedAt = session.StartedAt,
            CompletedAt = session.CompletedAt ?? DateTime.Now,
            ExecutionTime = session.ExecutionTime,
            IsSuccess = session.IsSuccess,
            ErrorMessage = session.ErrorMessage,
            TotalObjects = session.TotalObjects,
            AddedCount = session.AddedCount,
            DeletedCount = session.DeletedCount,
            ModifiedCount = session.ModifiedCount,
            UnchangedCount = session.UnchangedCount,
            AddedObjects = session.Results
                .Where(r => r.ChangeType == ChangeType.Added).ToList(),
            DeletedObjects = session.Results
                .Where(r => r.ChangeType == ChangeType.Deleted).ToList(),
            ModifiedObjects = session.Results
                .Where(r => r.ChangeType == ChangeType.Modified).ToList(),
            AllResults = session.Results
        };
    }
}