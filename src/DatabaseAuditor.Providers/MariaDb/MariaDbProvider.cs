using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Providers.MySql;
using MySqlConnector;
using System.Data;

namespace DatabaseAuditor.Providers.MariaDb;

public class MariaDbProvider : MySqlProvider
{
    protected override IDbConnection CreateConnection(ConnectionProfile connection)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = connection.Server,
            Port = (uint)connection.Port,
            Database = connection.DatabaseName,
            UserID = connection.Username,
            Password = connection.Password,
            ConnectionTimeout = 30,
            AllowZeroDateTime = true,
            ConvertZeroDateTime = true,
            // MariaDB-specific
            AllowPublicKeyRetrieval = true,
            SslMode = MySqlSslMode.None
        };
        return new MySqlConnection(builder.ConnectionString);
    }

    public override async Task<bool> TestConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = CreateConnection(connection);
            await ((MySqlConnection)conn).OpenAsync(cancellationToken);

            // Verify it is actually MariaDB
            var version = await QueryFirstOrDefaultAsync<string>(
                connection,
                "SELECT VERSION()",
                cancellationToken: cancellationToken);

            return conn.State == ConnectionState.Open
                && version != null
                && version.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public override async Task<List<TriggerSchema>> GetTriggersAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTriggers = null,
        CancellationToken cancellationToken = default)
    {
        // MariaDB stores trigger info slightly differently — ACTION_ORDER column added
        var sql = """
            SELECT
                t.TRIGGER_NAME          AS Name,
                t.TRIGGER_SCHEMA        AS SchemaName,
                t.EVENT_OBJECT_TABLE    AS TableName,
                t.EVENT_MANIPULATION    AS TriggerEvent,
                t.ACTION_TIMING         AS ActionTiming,
                t.ACTION_STATEMENT      AS Definition,
                t.CREATED               AS CreatedAt,
                t.ACTION_ORDER          AS ActionOrder
            FROM information_schema.TRIGGERS t
            WHERE t.TRIGGER_SCHEMA = @DatabaseName
            """;

        if (selectedTriggers != null && selectedTriggers.Count > 0)
        {
            sql += " AND CONCAT(t.TRIGGER_SCHEMA, '.', t.TRIGGER_NAME) IN @SelectedTriggers";
        }

        sql += " ORDER BY t.TRIGGER_SCHEMA, t.TRIGGER_NAME, t.ACTION_ORDER";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName, SelectedTriggers = selectedTriggers },
            cancellationToken);

        return rows.Select(r => new TriggerSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            TableName = r.TableName,
            TriggerEvent = r.TriggerEvent,
            ActionTiming = r.ActionTiming,
            Definition = r.Definition,
            NormalizedDefinition = NormalizeDefinition(r.Definition),
            IsEnabled = true,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public override Task<List<UserDefinedTableTypeSchema>> GetUserDefinedTableTypesAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTypes = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<UserDefinedTableTypeSchema>());
    }
}