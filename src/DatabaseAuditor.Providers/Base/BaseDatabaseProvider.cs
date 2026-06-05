namespace DatabaseAuditor.Providers.Base;

using Dapper;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Interfaces;
using System.Data;
using System.Text.RegularExpressions;

public abstract class BaseDatabaseProvider : IDatabaseProvider
{
    protected abstract IDbConnection CreateConnection(ConnectionProfile connection);

    protected async Task<List<T>> QueryAsync<T>(
        ConnectionProfile connection,
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection(connection);
        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        var result = await conn.QueryAsync<T>(command);
        return result.AsList();
    }

    protected async Task<T?> QueryFirstOrDefaultAsync<T>(
        ConnectionProfile connection,
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var conn = CreateConnection(connection);
        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        return await conn.QueryFirstOrDefaultAsync<T>(command);
    }

    protected static string NormalizeDefinition(string? definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
            return string.Empty;

        // Remove comments
        var result = Regex.Replace(definition, @"--[^\r\n]*", string.Empty);
        result = Regex.Replace(result, @"/\*.*?\*/", string.Empty,
            RegexOptions.Singleline);

        // Normalize whitespace
        result = Regex.Replace(result, @"\s+", " ");

        return result.Trim().ToUpperInvariant();
    }

    public virtual async Task<List<string>> GetTableNamesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
        => (await GetTablesAsync(connection, cancellationToken))
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();

    public virtual async Task<List<string>> GetProcedureNamesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
        => (await GetProceduresAsync(connection, cancellationToken))
            .Select(p => p.FullName)
            .OrderBy(n => n)
            .ToList();

    public virtual async Task<TableSchema?> GetTableAsync(
        ConnectionProfile connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
        => (await GetTablesAsync(connection, cancellationToken))
            .FirstOrDefault(t =>
                string.Equals(t.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

    public virtual async Task<ProcedureSchema?> GetProcedureAsync(
        ConnectionProfile connection,
        string schemaName,
        string procedureName,
        CancellationToken cancellationToken = default)
        => (await GetProceduresAsync(connection, cancellationToken))
            .FirstOrDefault(p =>
                string.Equals(p.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Name, procedureName, StringComparison.OrdinalIgnoreCase));

    public abstract Task<bool> TestConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TableSchema>> GetTablesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ColumnSchema>> GetColumnsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ProcedureSchema>> GetProceduresAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ViewSchema>> GetViewsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<FunctionSchema>> GetFunctionsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TriggerSchema>> GetTriggersAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ConstraintSchema>> GetConstraintsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<IndexSchema>> GetIndexesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);
}
