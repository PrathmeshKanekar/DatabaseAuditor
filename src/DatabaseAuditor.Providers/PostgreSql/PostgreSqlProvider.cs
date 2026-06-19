using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Providers.Base;
using Npgsql;
using System.Data;

namespace DatabaseAuditor.Providers.PostgreSql;

public class PostgreSqlProvider : BaseDatabaseProvider
{
    protected override IDbConnection CreateConnection(ConnectionProfile connection)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = connection.Server,
            Database = connection.DatabaseName,
            Username = connection.Username,
            Password = connection.Password,
            Timeout = 30,
            CommandTimeout = 120
        };
        if (connection.Port > 0)
        {
            builder.Port = connection.Port;
        }
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
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                t.table_name        AS Name,
                t.table_schema      AS SchemaName,
                pg_stat_user_tables.n_live_tup AS TotalRows
            FROM information_schema.tables t
            LEFT JOIN pg_stat_user_tables
                ON pg_stat_user_tables.relname = t.table_name
                AND pg_stat_user_tables.schemaname = t.table_schema
            WHERE t.table_type = 'BASE TABLE'
              AND t.table_schema NOT IN ('pg_catalog','information_schema')
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (t.table_schema || '.' || t.table_name) IN @SelectedTables ";
        }

        sql += " ORDER BY t.table_schema, t.table_name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        return rows.Select(r => new TableSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
            TotalRows = r.totalrows ?? 0
        }).ToList();
    }

    public override async Task<List<ColumnSchema>> GetColumnsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                c.column_name           AS Name,
                c.table_schema          AS SchemaName,
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
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (c.table_schema || '.' || c.table_name) IN @SelectedTables ";
        }

        sql += " ORDER BY c.table_schema, c.table_name, c.ordinal_position";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        return rows.Select(r => new ColumnSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
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
        IReadOnlyCollection<string>? selectedProcedures = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                r.routine_name      AS Name,
                r.routine_schema    AS SchemaName,
                r.routine_definition AS Definition,
                r.created           AS CreatedAt,
                r.last_altered      AS ModifiedAt
            FROM information_schema.routines r
            WHERE r.routine_type = 'PROCEDURE'
              AND r.routine_schema NOT IN ('pg_catalog','information_schema')
            """;

        if (selectedProcedures != null && selectedProcedures.Count > 0)
        {
            sql += " AND (r.routine_schema || '.' || r.routine_name) IN @SelectedProcedures ";
        }

        sql += " ORDER BY r.routine_schema, r.routine_name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedProcedures = selectedProcedures }, cancellationToken);
        var procedures = rows.Select(r => new ProcedureSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
            Definition = r.definition,
            NormalizedDefinition = NormalizeDefinition(r.definition),
            CreatedAt = r.createdat,
            ModifiedAt = r.modifiedat
        }).ToList();

        // Load parameters
        var paramSql = """
            SELECT
                r.routine_name AS RoutineName,
                r.routine_schema AS RoutineSchema,
                p.parameter_name AS Name,
                p.data_type AS DataType,
                p.character_maximum_length AS MaxLength,
                p.ordinal_position AS OrdinalPosition,
                p.parameter_mode AS ParameterMode
            FROM information_schema.parameters p
            JOIN information_schema.routines r
                ON p.specific_name = r.specific_name
            WHERE r.routine_type = 'PROCEDURE'
              AND r.routine_schema NOT IN ('pg_catalog','information_schema')
            """;

        if (selectedProcedures != null && selectedProcedures.Count > 0)
        {
            paramSql += " AND (r.routine_schema || '.' || r.routine_name) IN @SelectedProcedures ";
        }

        var paramRows = await QueryAsync<dynamic>(connection, paramSql, new { SelectedProcedures = selectedProcedures }, cancellationToken);
        var paramMap = paramRows.GroupBy(p => $"{p.routineschema}.{p.routinename}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var proc in procedures)
        {
            if (paramMap.TryGetValue(proc.FullName, out var paramsList))
            {
                proc.Parameters = paramsList.Select(p => new ParameterSchema
                {
                    Name = p.name ?? string.Empty,
                    DataType = p.maxlength != null && p.maxlength > 0 ? $"{p.datatype}({p.maxlength})" : (string)p.datatype,
                    OrdinalPosition = p.ordinal_position,
                    IsOutput = p.parametermode == "INOUT" || p.parametermode == "OUT"
                }).OrderBy(p => p.OrdinalPosition).ToList();
            }
        }

        return procedures;
    }

    public override async Task<List<ViewSchema>> GetViewsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedViews = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                v.table_name        AS Name,
                v.table_schema      AS SchemaName,
                v.view_definition   AS Definition,
                v.is_updatable      AS IsUpdatable
            FROM information_schema.views v
            WHERE v.table_schema NOT IN ('pg_catalog','information_schema')
            """;

        if (selectedViews != null && selectedViews.Count > 0)
        {
            sql += " AND (v.table_schema || '.' || v.table_name) IN @SelectedViews ";
        }

        sql += " ORDER BY v.table_schema, v.table_name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedViews = selectedViews }, cancellationToken);

        return rows.Select(r => new ViewSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
            Definition = r.definition,
            NormalizedDefinition = NormalizeDefinition(r.definition),
            IsUpdatable = r.isupdatable == "YES"
        }).ToList();
    }

    public override async Task<List<FunctionSchema>> GetFunctionsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedFunctions = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                r.routine_name          AS Name,
                r.routine_schema        AS SchemaName,
                r.routine_definition    AS Definition,
                r.data_type             AS ReturnType,
                r.routine_type          AS FunctionType,
                r.created               AS CreatedAt,
                r.last_altered          AS ModifiedAt
            FROM information_schema.routines r
            WHERE r.routine_type = 'FUNCTION'
              AND r.routine_schema NOT IN ('pg_catalog','information_schema')
            """;

        if (selectedFunctions != null && selectedFunctions.Count > 0)
        {
            sql += " AND (r.routine_schema || '.' || r.routine_name) IN @SelectedFunctions ";
        }

        sql += " ORDER BY r.routine_schema, r.routine_name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedFunctions = selectedFunctions }, cancellationToken);

        return rows.Select(r => new FunctionSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
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
        IReadOnlyCollection<string>? selectedTriggers = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                t.trigger_name          AS Name,
                t.trigger_schema        AS SchemaName,
                t.event_object_table    AS TableName,
                t.event_manipulation    AS TriggerEvent,
                t.action_timing         AS ActionTiming,
                t.action_statement      AS Definition
            FROM information_schema.triggers t
            WHERE t.trigger_schema NOT IN ('pg_catalog','information_schema')
            """;

        if (selectedTriggers != null && selectedTriggers.Count > 0)
        {
            sql += " AND (t.trigger_schema || '.' || t.trigger_name) IN @SelectedTriggers ";
        }

        sql += " ORDER BY t.trigger_schema, t.trigger_name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTriggers = selectedTriggers }, cancellationToken);

        return rows.Select(r => new TriggerSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
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
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                tc.constraint_name      AS Name,
                tc.table_schema         AS SchemaName,
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
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (tc.table_schema || '.' || tc.table_name) IN @SelectedTables ";
        }

        sql += " ORDER BY tc.table_schema, tc.table_name, tc.constraint_name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        var grouped = rows.GroupBy(r => new { Name = (string)r.name, SchemaName = (string)r.schemaname, TableName = (string)r.tablename });

        return grouped.Select(g =>
        {
            var first = g.First();
            var columnNames = string.Join(",", g.Select(x => (string)x.columnname).Where(c => !string.IsNullOrEmpty(c)));
            var referencedColumns = string.Join(",", g.Select(x => (string)x.referencedcolumn).Where(c => !string.IsNullOrEmpty(c)));

            return new ConstraintSchema
            {
                Name = first.name,
                SchemaName = first.schemaname,
                TableName = first.tablename,
                ConstraintType = first.constrainttype,
                ColumnName = string.IsNullOrEmpty(columnNames) ? null : columnNames,
                ReferencedTable = first.referencedtable,
                ReferencedColumn = string.IsNullOrEmpty(referencedColumns) ? null : referencedColumns,
                CheckClause = first.checkclause
            };
        }).ToList();
    }

    public override async Task<List<IndexSchema>> GetIndexesAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                i.relname               AS Name,
                n.nspname               AS SchemaName,
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
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (n.nspname || '.' || t.relname) IN @SelectedTables ";
        }

        sql += """
            GROUP BY i.relname, n.nspname, t.relname,
                     am.amname, ix.indisunique, ix.indisprimary,
                     ix.indisvalid, ix.indisclustered
            ORDER BY n.nspname, t.relname, i.relname
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        return rows.Select(r => new IndexSchema
        {
            Name = r.name,
            SchemaName = r.schemaname,
            TableName = r.tablename,
            IndexType = r.indextype,
            IsUnique = (bool)r.isunique,
            IsPrimaryKey = (bool)r.isprimarykey,
            IsDisabled = (bool)r.isdisabled,
            IsClustered = (bool)r.isclustered,
            Columns = (r.columns as string)?.Split(',').ToList() ?? new List<string>()
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