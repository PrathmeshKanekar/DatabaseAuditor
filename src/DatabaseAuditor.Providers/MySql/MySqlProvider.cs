namespace DatabaseAuditor.Providers.MySql;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Providers.Base;
using MySqlConnector;
using System.Data;

public class MySqlProvider : BaseDatabaseProvider
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
            ConvertZeroDateTime = true
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
            return conn.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<List<TableSchema>> GetTablesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                t.TABLE_NAME        AS Name,
                t.TABLE_SCHEMA      AS SchemaName,
                t.ENGINE            AS Engine,
                t.TABLE_COLLATION   AS Collation,
                t.TABLE_ROWS        AS TotalRows
            FROM information_schema.TABLES t
            WHERE t.TABLE_TYPE = 'BASE TABLE'
              AND t.TABLE_SCHEMA = @DatabaseName
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new TableSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            Engine = r.Engine,
            Collation = r.Collation,
            TotalRows = r.TotalRows ?? 0
        }).ToList();
    }

    public override async Task<List<ColumnSchema>> GetColumnsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.COLUMN_NAME           AS Name,
                c.TABLE_SCHEMA          AS SchemaName,
                c.TABLE_NAME            AS TableName,
                c.ORDINAL_POSITION      AS OrdinalPosition,
                c.DATA_TYPE             AS DataType,
                c.CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                c.NUMERIC_PRECISION     AS Precision,
                c.NUMERIC_SCALE         AS Scale,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                CASE WHEN c.EXTRA LIKE '%auto_increment%' THEN 1 ELSE 0 END AS IsIdentity,
                CASE WHEN c.EXTRA LIKE '%VIRTUAL%'
                       OR c.EXTRA LIKE '%STORED%' THEN 1 ELSE 0 END AS IsComputed,
                c.COLUMN_DEFAULT        AS DefaultValue,
                c.COLLATION_NAME        AS CollationName,
                CASE WHEN c.COLUMN_KEY = 'PRI' THEN 1 ELSE 0 END AS IsPrimaryKey,
                CASE WHEN c.COLUMN_KEY = 'MUL' THEN 1 ELSE 0 END AS IsForeignKey
            FROM information_schema.COLUMNS c
            WHERE c.TABLE_SCHEMA = @DatabaseName
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new ColumnSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            TableName = r.TableName,
            OrdinalPosition = r.OrdinalPosition,
            DataType = r.DataType,
            MaxLength = r.MaxLength,
            Precision = r.Precision,
            Scale = r.Scale,
            IsNullable = r.IsNullable == 1,
            IsIdentity = r.IsIdentity == 1,
            IsComputed = r.IsComputed == 1,
            DefaultValue = r.DefaultValue,
            CollationName = r.CollationName,
            IsPrimaryKey = r.IsPrimaryKey == 1,
            IsForeignKey = r.IsForeignKey == 1
        }).ToList();
    }

    public override async Task<List<ProcedureSchema>> GetProceduresAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                r.ROUTINE_NAME          AS Name,
                r.ROUTINE_SCHEMA        AS SchemaName,
                r.ROUTINE_DEFINITION    AS Definition,
                r.CREATED               AS CreatedAt,
                r.LAST_ALTERED          AS ModifiedAt
            FROM information_schema.ROUTINES r
            WHERE r.ROUTINE_TYPE = 'PROCEDURE'
              AND r.ROUTINE_SCHEMA = @DatabaseName
            ORDER BY r.ROUTINE_SCHEMA, r.ROUTINE_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new ProcedureSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            Definition = r.Definition,
            NormalizedDefinition = NormalizeDefinition(r.Definition),
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt
        }).ToList();
    }

    public override async Task<List<ViewSchema>> GetViewsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                v.TABLE_NAME            AS Name,
                v.TABLE_SCHEMA          AS SchemaName,
                v.VIEW_DEFINITION       AS Definition,
                v.IS_UPDATABLE          AS IsUpdatable
            FROM information_schema.VIEWS v
            WHERE v.TABLE_SCHEMA = @DatabaseName
            ORDER BY v.TABLE_SCHEMA, v.TABLE_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new ViewSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            Definition = r.Definition,
            NormalizedDefinition = NormalizeDefinition(r.Definition),
            IsUpdatable = r.IsUpdatable == "YES"
        }).ToList();
    }

    public override async Task<List<FunctionSchema>> GetFunctionsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                r.ROUTINE_NAME          AS Name,
                r.ROUTINE_SCHEMA        AS SchemaName,
                r.ROUTINE_DEFINITION    AS Definition,
                r.DATA_TYPE             AS ReturnType,
                r.ROUTINE_TYPE          AS FunctionType,
                r.CREATED               AS CreatedAt,
                r.LAST_ALTERED          AS ModifiedAt
            FROM information_schema.ROUTINES r
            WHERE r.ROUTINE_TYPE = 'FUNCTION'
              AND r.ROUTINE_SCHEMA = @DatabaseName
            ORDER BY r.ROUTINE_SCHEMA, r.ROUTINE_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new FunctionSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            Definition = r.Definition,
            NormalizedDefinition = NormalizeDefinition(r.Definition),
            ReturnType = r.ReturnType,
            FunctionType = r.FunctionType,
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt
        }).ToList();
    }

    public override async Task<List<TriggerSchema>> GetTriggersAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                t.TRIGGER_NAME          AS Name,
                t.TRIGGER_SCHEMA        AS SchemaName,
                t.EVENT_OBJECT_TABLE    AS TableName,
                t.EVENT_MANIPULATION    AS TriggerEvent,
                t.ACTION_TIMING         AS ActionTiming,
                t.ACTION_STATEMENT      AS Definition,
                t.CREATED               AS CreatedAt
            FROM information_schema.TRIGGERS t
            WHERE t.TRIGGER_SCHEMA = @DatabaseName
            ORDER BY t.TRIGGER_SCHEMA, t.TRIGGER_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
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

    public override async Task<List<ConstraintSchema>> GetConstraintsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                tc.CONSTRAINT_NAME      AS Name,
                tc.TABLE_SCHEMA         AS SchemaName,
                tc.TABLE_NAME           AS TableName,
                tc.CONSTRAINT_TYPE      AS ConstraintType,
                kcu.COLUMN_NAME         AS ColumnName,
                kcu.REFERENCED_TABLE_NAME  AS ReferencedTable,
                kcu.REFERENCED_COLUMN_NAME AS ReferencedColumn,
                cc.CHECK_CLAUSE         AS CheckClause
            FROM information_schema.TABLE_CONSTRAINTS tc
            LEFT JOIN information_schema.KEY_COLUMN_USAGE kcu
                ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                AND kcu.TABLE_SCHEMA = tc.TABLE_SCHEMA
                AND kcu.TABLE_NAME = tc.TABLE_NAME
            LEFT JOIN information_schema.CHECK_CONSTRAINTS cc
                ON cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                AND cc.CONSTRAINT_SCHEMA = tc.TABLE_SCHEMA
            WHERE tc.TABLE_SCHEMA = @DatabaseName
            ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new ConstraintSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            TableName = r.TableName,
            ConstraintType = r.ConstraintType,
            ColumnName = r.ColumnName,
            ReferencedTable = r.ReferencedTable,
            ReferencedColumn = r.ReferencedColumn,
            CheckClause = r.CheckClause
        }).ToList();
    }

    public override async Task<List<IndexSchema>> GetIndexesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                s.INDEX_NAME            AS Name,
                s.TABLE_SCHEMA          AS SchemaName,
                s.TABLE_NAME            AS TableName,
                s.INDEX_TYPE            AS IndexType,
                CASE WHEN s.NON_UNIQUE = 0 THEN 1 ELSE 0 END AS IsUnique,
                CASE WHEN s.INDEX_NAME = 'PRIMARY' THEN 1 ELSE 0 END AS IsPrimaryKey,
                0                       AS IsDisabled,
                CASE WHEN s.INDEX_NAME = 'PRIMARY' THEN 1 ELSE 0 END AS IsClustered,
                GROUP_CONCAT(s.COLUMN_NAME ORDER BY s.SEQ_IN_INDEX) AS Columns
            FROM information_schema.STATISTICS s
            WHERE s.TABLE_SCHEMA = @DatabaseName
            GROUP BY s.INDEX_NAME, s.TABLE_SCHEMA, s.TABLE_NAME,
                     s.INDEX_TYPE, s.NON_UNIQUE
            ORDER BY s.TABLE_SCHEMA, s.TABLE_NAME, s.INDEX_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { DatabaseName = connection.DatabaseName },
            cancellationToken);

        return rows.Select(r => new IndexSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            TableName = r.TableName,
            IndexType = r.IndexType,
            IsUnique = r.IsUnique == 1,
            IsPrimaryKey = r.IsPrimaryKey == 1,
            IsDisabled = false,
            IsClustered = r.IsClustered == 1,
            Columns = ((string?)r.Columns)?.Split(',').ToList() ?? []
        }).ToList();
    }
}