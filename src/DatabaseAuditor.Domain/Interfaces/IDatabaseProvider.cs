namespace DatabaseAuditor.Domain.Interfaces;

using DatabaseAuditor.Domain.Entities;

public interface IDatabaseProvider
{
    Task<bool> TestConnectionAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<string>> GetTableNamesAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<string>> GetProcedureNamesAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<TableSchema?> GetTableAsync(ConnectionProfile connection, string schemaName, string tableName, CancellationToken cancellationToken = default);
    Task<ProcedureSchema?> GetProcedureAsync(ConnectionProfile connection, string schemaName, string procedureName, CancellationToken cancellationToken = default);
    Task<List<TableSchema>> GetTablesAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ColumnSchema>> GetColumnsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ProcedureSchema>> GetProceduresAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ViewSchema>> GetViewsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<FunctionSchema>> GetFunctionsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<TriggerSchema>> GetTriggersAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ConstraintSchema>> GetConstraintsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<IndexSchema>> GetIndexesAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
}
