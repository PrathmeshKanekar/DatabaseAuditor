using Dapper;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Interfaces;
using System.Data;
using System.Text.RegularExpressions;

namespace DatabaseAuditor.Providers.Base;

public abstract class BaseDatabaseProvider : IDatabaseProvider
{
    protected abstract IDbConnection CreateConnection(ConnectionProfile connection);

    protected async Task<List<T>> QueryAsync<T>(
        ConnectionProfile connection,
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        Serilog.Log.Information(sql);
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
        Serilog.Log.Information(sql);
        using var conn = CreateConnection(connection);
        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        return await conn.QueryFirstOrDefaultAsync<T?>(command);
    }

    protected static string NormalizeDefinition(string? definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
            return string.Empty;

        // Remove comments
        var result = Regex.Replace(definition, @"--[^\r\n]*", string.Empty);
        result = Regex.Replace(result, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        // Normalize casing
        result = result.ToUpperInvariant();

        // Replace all tabs, line breaks, carriage returns with spaces
        result = result.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

        // Normalize whitespace
        result = Regex.Replace(result, @"\s+", " ");

        // Ignore formatting around punctuation/operators
        result = Regex.Replace(result, @"\s*([,()=<>+\-*/])\s*", "$1");

        return result.Trim();
    }

    public virtual async Task<List<string>> GetTableNamesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
        => (await GetTablesAsync(connection, null, cancellationToken))
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();

    public virtual async Task<List<string>> GetProcedureNamesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
        => (await GetProceduresAsync(connection, null, cancellationToken))
            .Select(p => p.FullName)
            .OrderBy(n => n)
            .ToList();

    public virtual async Task<TableSchema?> GetTableAsync(
        ConnectionProfile connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
        => (await GetTablesAsync(connection, [ $"{schemaName}.{tableName}" ], cancellationToken))
            .FirstOrDefault(t =>
                string.Equals(t.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));

    public virtual async Task<ProcedureSchema?> GetProcedureAsync(
        ConnectionProfile connection,
        string schemaName,
        string procedureName,
        CancellationToken cancellationToken = default)
        => (await GetProceduresAsync(connection, [ $"{schemaName}.{procedureName}" ], cancellationToken))
            .FirstOrDefault(p =>
                string.Equals(p.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Name, procedureName, StringComparison.OrdinalIgnoreCase));

    public abstract Task<bool> TestConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TableSchema>> GetTablesAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ColumnSchema>> GetColumnsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ProcedureSchema>> GetProceduresAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedProcedures = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ViewSchema>> GetViewsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedViews = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<FunctionSchema>> GetFunctionsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedFunctions = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<TriggerSchema>> GetTriggersAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTriggers = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<ConstraintSchema>> GetConstraintsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<IndexSchema>> GetIndexesAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default);

    public abstract Task<List<UserDefinedTableTypeSchema>> GetUserDefinedTableTypesAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTypes = null,
        CancellationToken cancellationToken = default);

    public virtual async Task<List<string>> GetUserDefinedTableTypeNamesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
        => (await GetUserDefinedTableTypesAsync(connection, null, cancellationToken))
            .Select(t => t.FullName)
            .OrderBy(n => n)
            .ToList();

    public virtual async Task ExecuteSqlAsync(
        ConnectionProfile connection,
        string sql,
        CancellationToken cancellationToken = default)
    {
        Serilog.Log.Information("Executing SQL on {Database}: {Sql}", connection.DatabaseName, sql);
        using var conn = CreateConnection(connection);
        if (conn is System.Data.Common.DbConnection dbConn)
        {
            if (dbConn.State != ConnectionState.Open)
                await dbConn.OpenAsync(cancellationToken);
        }
        else
        {
            if (conn.State != ConnectionState.Open)
                conn.Open();
        }
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(command);
    }
}
