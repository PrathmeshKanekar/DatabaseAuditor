namespace DatabaseAuditor.Providers.PostgreSql;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Providers.Base;
using Npgsql;
using System.Data;

public class PostgreSqlProvider : BaseDatabaseProvider
{
    protected override IDbConnection CreateConnection(ConnectionProfile connection)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = connection.Server,
            Port = connection.Port,
            Database = connection.DatabaseName,
            Username = connection.Username,
            Password = connection.Password,
            Timeout = 30,
            CommandTimeout = 120
        };
        return new NpgsqlConnection(builder.ConnectionString);
    }

    public override async Task<bool> TestConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = CreateConnection(connection);
            await ((NpgsqlConnection)conn).OpenAsync(cancellationToken);
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
                t.table_name        AS Name,
                t.table_schema      AS Schema,
                pg_stat_user_tables.n_live_tup AS RowCount
            FROM information_schema.tables t
            LEFT JOIN pg_stat_user_tables
                ON pg_stat_user_tables.relname = t.table_name
                AND pg_stat_user_tables.schemaname = t.table_schema
            WHERE t.table_type = 'BASE TABLE'
              AND t.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY t.table_schema, t.table_name
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new TableSchema
        {
            Name = r.name,
            Schema = r.schema,
            RowCount = r.rowcount ?? 0
        }).ToList();
    }

    public override async Task<List<ColumnSchema>> GetColumnsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.column_name           AS Name,
                c.table_schema          AS Schema,
                c.table_name            AS TableName,
                c.ordinal_position      AS OrdinalPosition,
                c.data_type             AS DataType,
                c.character_maximum_length AS MaxLength,
                c.numeric_precision     AS Precision,
                c.numeric_scale         AS Scale,
                CASE WHEN c.is_nullable = 'YES' THEN true ELSE false END AS IsNullable,
                CASE WHEN c.column_default LIKE 'nextval%' THEN true ELSE false END AS IsIdentity,
                c.column_default        AS DefaultValue,
                c.collation_name        AS CollationName,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS IsPrimaryKey,
                CASE WHEN fk.column_name IS NOT NULL THEN true ELSE false END AS IsForeignKey
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.table_name, ku.column_name, ku.table_schema
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON pk.table_name = c.table_name
                AND pk.column_name = c.column_name
                AND pk.table_schema = c.table_schema
            LEFT JOIN (
                SELECT ku.table_name, ku.column_name, ku.table_schema
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY'
            ) fk ON fk.table_name = c.table_name
                AND fk.column_name = c.column_name
                AND fk.table_schema = c.table_schema
            WHERE c.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY c.table_schema, c.table_name, c.ordinal_position
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new ColumnSchema
        {
            Name = r.name,
            Schema = r.schema,
            TableName = r.tablename,
            OrdinalPosition = r.ordinalposition,
            DataType = r.datatype,
            MaxLength = r.maxlength,
            Precision = r.precision,
            Scale = r.scale,
            IsNullable = r.isnullable,
            IsIdentity = r.isidentity,
            DefaultValue = r.defaultvalue,
            CollationName = r.collationname,
            IsPrimaryKey = r.isprimarykey,
            IsForeignKey = r.isforeignkey
        }).ToList();
    }

    public override async Task<List<ProcedureSchema>> GetProceduresAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                r.routine_name      AS Name,
                r.routine_schema    AS Schema,
                r.routine_definition AS Definition,
                r.created           AS CreatedAt,
                r.last_altered      AS ModifiedAt
            FROM information_schema.routines r
            WHERE r.routine_type = 'PROCEDURE'
              AND r.routine_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY r.routine_schema, r.routine_name
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new ProcedureSchema
        {
            Name = r.name,
            Schema = r.schema,
            Definition = r.definition,
            NormalizedDefinition = NormalizeDefinition(r.definition),
            CreatedAt = r.createdat,
            ModifiedAt = r.modifiedat
        }).ToList();
    }

    public override async Task<List<ViewSchema>> GetViewsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                v.table_name        AS Name,
                v.table_schema      AS Schema,
                v.view_definition   AS Definition,
                v.is_updatable      AS IsUpdatable
            FROM information_schema.views v
            WHERE v.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY v.table_schema, v.table_name
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new ViewSchema
        {
            Name = r.name,
            Schema = r.schema,
            Definition = r.definition,
            NormalizedDefinition = NormalizeDefinition(r.definition),
            IsUpdatable = r.isupdatable == "YES"
        }).ToList();
    }

    public override async Task<List<FunctionSchema>> GetFunctionsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                r.routine_name          AS Name,
                r.routine_schema        AS Schema,
                r.routine_definition    AS Definition,
                r.data_type             AS ReturnType,
                r.routine_type          AS FunctionType,
                r.created               AS CreatedAt,
                r.last_altered          AS ModifiedAt
            FROM information_schema.routines r
            WHERE r.routine_type = 'FUNCTION'
              AND r.routine_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY r.routine_schema, r.routine_name
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new FunctionSchema
        {
            Name = r.name,
            Schema = r.schema,
            Definition = r.definition,
            NormalizedDefinition = NormalizeDefinition(r.definition),
            ReturnType = r.returntype,
            FunctionType = r.functiontype,
            CreatedAt = r.createdat,
            ModifiedAt = r.modifiedat
        }).ToList();
    }

    public override async Task<List<TriggerSchema>> GetTriggersAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                t.trigger_name          AS Name,
                t.trigger_schema        AS Schema,
                t.event_object_table    AS TableName,
                t.event_manipulation    AS TriggerEvent,
                t.action_timing         AS ActionTiming,
                t.action_statement      AS Definition
            FROM information_schema.triggers t
            WHERE t.trigger_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY t.trigger_schema, t.trigger_name
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new TriggerSchema
        {
            Name = r.name,
            Schema = r.schema,
            TableName = r.tablename,
            TriggerEvent = r.triggerevent,
            ActionTiming = r.actiontiming,
            Definition = r.definition,
            NormalizedDefinition = NormalizeDefinition(r.definition),
            IsEnabled = true
        }).ToList();
    }

    public override async Task<List<ConstraintSchema>> GetConstraintsAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                tc.constraint_name      AS Name,
                tc.table_schema         AS Schema,
                tc.table_name           AS TableName,
                tc.constraint_type      AS ConstraintType,
                kcu.column_name         AS ColumnName,
                ccu.table_name          AS ReferencedTable,
                ccu.column_name         AS ReferencedColumn,
                cc.check_clause         AS CheckClause
            FROM information_schema.table_constraints tc
            LEFT JOIN information_schema.key_column_usage kcu
                ON kcu.constraint_name = tc.constraint_name
                AND kcu.table_schema = tc.table_schema
            LEFT JOIN information_schema.referential_constraints rc
                ON rc.constraint_name = tc.constraint_name
            LEFT JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = rc.unique_constraint_name
            LEFT JOIN information_schema.check_constraints cc
                ON cc.constraint_name = tc.constraint_name
            WHERE tc.table_schema NOT IN ('pg_catalog','information_schema')
            ORDER BY tc.table_schema, tc.table_name, tc.constraint_name
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new ConstraintSchema
        {
            Name = r.name,
            Schema = r.schema,
            TableName = r.tablename,
            ConstraintType = r.constrainttype,
            ColumnName = r.columnname,
            ReferencedTable = r.referencedtable,
            ReferencedColumn = r.referencedcolumn,
            CheckClause = r.checkclause
        }).ToList();
    }

    public override async Task<List<IndexSchema>> GetIndexesAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                i.relname               AS Name,
                n.nspname               AS Schema,
                t.relname               AS TableName,
                am.amname               AS IndexType,
                ix.indisunique          AS IsUnique,
                ix.indisprimary         AS IsPrimaryKey,
                NOT ix.indisvalid       AS IsDisabled,
                ix.indisclustered       AS IsClustered,
                STRING_AGG(a.attname, ',' ORDER BY array_position(ix.indkey, a.attnum)) AS Columns
            FROM pg_index ix
            JOIN pg_class i  ON i.oid  = ix.indexrelid
            JOIN pg_class t  ON t.oid  = ix.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_am am    ON am.oid  = i.relam
            JOIN pg_attribute a ON a.attrelid = t.oid
                AND a.attnum = ANY(ix.indkey)
            WHERE n.nspname NOT IN ('pg_catalog','information_schema')
            GROUP BY i.relname, n.nspname, t.relname,
                     am.amname, ix.indisunique, ix.indisprimary,
                     ix.indisvalid, ix.indisclustered
            ORDER BY n.nspname, t.relname, i.relname
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, cancellationToken: cancellationToken);

        return rows.Select(r => new IndexSchema
        {
            Name = r.name,
            Schema = r.schema,
            TableName = r.tablename,
            IndexType = r.indextype,
            IsUnique = (bool)r.isunique,
            IsPrimaryKey = (bool)r.isprimarykey,
            IsDisabled = (bool)r.isdisabled,
            IsClustered = (bool)r.isclustered,
            Columns = r.columns?.Split(',').ToList() ?? []
        }).ToList();
    }
}