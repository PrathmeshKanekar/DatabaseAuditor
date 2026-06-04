namespace DatabaseAuditor.Domain.Interfaces;

using DatabaseAuditor.Domain.Entities;

public interface IDatabaseProvider
{
    Task<bool> TestConnectionAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<TableSchema>> GetTablesAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ColumnSchema>> GetColumnsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ProcedureSchema>> GetProceduresAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ViewSchema>> GetViewsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<FunctionSchema>> GetFunctionsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<TriggerSchema>> GetTriggersAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<ConstraintSchema>> GetConstraintsAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
    Task<List<IndexSchema>> GetIndexesAsync(ConnectionProfile connection, CancellationToken cancellationToken = default);
}