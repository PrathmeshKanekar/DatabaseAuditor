using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Providers.Base;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DatabaseAuditor.Providers.SqlServer;

public class SqlServerProvider : BaseDatabaseProvider
{
    protected override IDbConnection CreateConnection(ConnectionProfile connection)
    {
        var dataSource = connection.Port > 0
            ? $"{connection.Server},{connection.Port}"
            : connection.Server;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = connection.DatabaseName,
            UserID = connection.Username,
            Password = connection.Password,
            ConnectTimeout = 30,
            TrustServerCertificate = true,
            Encrypt = false
        };
        return new SqlConnection(builder.ConnectionString);
    }

    public override async Task<bool> TestConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = CreateConnection(connection);
            if (conn is System.Data.Common.DbConnection dbConn)
                await dbConn.OpenAsync(cancellationToken);
            else
                conn.Open();
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
                t.TABLE_NAME        AS Name,
                t.TABLE_SCHEMA      AS SchemaName,
                p.value             AS Collation,
                SUM(par.rows)       AS TotalRows
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.tables st
                ON st.name = t.TABLE_NAME
            LEFT JOIN sys.extended_properties p
                ON p.major_id = st.object_id AND p.name = 'MS_Description'
            LEFT JOIN sys.partitions par
                ON par.object_id = st.object_id AND par.index_id IN (0,1)
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (t.TABLE_SCHEMA + '.' + t.TABLE_NAME) IN @SelectedTables ";
        }

        sql += """
            GROUP BY t.TABLE_NAME, t.TABLE_SCHEMA, p.value
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        return rows.Select(r => new TableSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            Collation = r.Collation,
            TotalRows = r.TotalRows ?? 0
        }).ToList();
    }

    public override async Task<List<ColumnSchema>> GetColumnsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
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
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA+'.'+c.TABLE_NAME),
                    c.COLUMN_NAME,'IsIdentity') = 1 THEN 1 ELSE 0 END AS IsIdentity,
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA+'.'+c.TABLE_NAME),
                    c.COLUMN_NAME,'IsComputed') = 1 THEN 1 ELSE 0 END AS IsComputed,
                c.COLUMN_DEFAULT        AS DefaultValue,
                c.COLLATION_NAME        AS CollationName,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_NAME, ku.COLUMN_NAME, ku.TABLE_SCHEMA
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON pk.TABLE_NAME = c.TABLE_NAME
                AND pk.COLUMN_NAME = c.COLUMN_NAME
                AND pk.TABLE_SCHEMA = c.TABLE_SCHEMA
            LEFT JOIN (
                SELECT ku.TABLE_NAME, ku.COLUMN_NAME, ku.TABLE_SCHEMA
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            ) fk ON fk.TABLE_NAME = c.TABLE_NAME
                AND fk.COLUMN_NAME = c.COLUMN_NAME
                AND fk.TABLE_SCHEMA = c.TABLE_SCHEMA
            WHERE 1=1
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (c.TABLE_SCHEMA + '.' + c.TABLE_NAME) IN @SelectedTables ";
        }

        sql += " ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

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
        IReadOnlyCollection<string>? selectedProcedures = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                r.ROUTINE_NAME      AS Name,
                r.ROUTINE_SCHEMA    AS SchemaName,
                r.ROUTINE_DEFINITION AS Definition,
                r.CREATED           AS CreatedAt,
                r.LAST_ALTERED      AS ModifiedAt
            FROM INFORMATION_SCHEMA.ROUTINES r
            WHERE r.ROUTINE_TYPE = 'PROCEDURE'
            """;

        if (selectedProcedures != null && selectedProcedures.Count > 0)
        {
            sql += " AND (r.ROUTINE_SCHEMA + '.' + r.ROUTINE_NAME) IN @SelectedProcedures ";
        }

        sql += " ORDER BY r.ROUTINE_SCHEMA, r.ROUTINE_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedProcedures = selectedProcedures }, cancellationToken);
        var procedures = rows.Select(r => new ProcedureSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            Definition = r.Definition,
            NormalizedDefinition = NormalizeDefinition(r.Definition),
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt
        }).ToList();

        // Load parameters
        var paramSql = """
            SELECT
                SPECIFIC_NAME AS RoutineName,
                SPECIFIC_SCHEMA AS RoutineSchema,
                PARAMETER_NAME AS Name,
                DATA_TYPE AS DataType,
                CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                ORDINAL_POSITION AS OrdinalPosition,
                PARAMETER_MODE AS ParameterMode
            FROM INFORMATION_SCHEMA.PARAMETERS
            WHERE 1=1
            """;

        if (selectedProcedures != null && selectedProcedures.Count > 0)
        {
            paramSql += " AND (SPECIFIC_SCHEMA + '.' + SPECIFIC_NAME) IN @SelectedProcedures ";
        }

        var paramRows = await QueryAsync<dynamic>(connection, paramSql, new { SelectedProcedures = selectedProcedures }, cancellationToken);
        var paramMap = paramRows.GroupBy(p => $"{p.RoutineSchema}.{p.RoutineName}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var proc in procedures)
        {
            if (paramMap.TryGetValue(proc.FullName, out var paramsList))
            {
                proc.Parameters = paramsList.Select(p => new ParameterSchema
                {
                    Name = p.Name ?? string.Empty,
                    DataType = p.MaxLength != null && p.MaxLength > 0 ? $"{p.DataType}({p.MaxLength})" : (string)p.DataType,
                    OrdinalPosition = p.OrdinalPosition,
                    IsOutput = p.ParameterMode == "INOUT" || p.ParameterMode == "OUT"
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
                v.TABLE_NAME        AS Name,
                v.TABLE_SCHEMA      AS SchemaName,
                v.VIEW_DEFINITION   AS Definition,
                v.IS_UPDATABLE      AS IsUpdatable
            FROM INFORMATION_SCHEMA.VIEWS v
            WHERE 1=1
            """;

        if (selectedViews != null && selectedViews.Count > 0)
        {
            sql += " AND (v.TABLE_SCHEMA + '.' + v.TABLE_NAME) IN @SelectedViews ";
        }

        sql += " ORDER BY v.TABLE_SCHEMA, v.TABLE_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedViews = selectedViews }, cancellationToken);

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
        IReadOnlyCollection<string>? selectedFunctions = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                r.ROUTINE_NAME          AS Name,
                r.ROUTINE_SCHEMA        AS SchemaName,
                r.ROUTINE_DEFINITION    AS Definition,
                r.DATA_TYPE             AS ReturnType,
                r.ROUTINE_TYPE          AS FunctionType,
                r.CREATED               AS CreatedAt,
                r.LAST_ALTERED          AS ModifiedAt
            FROM INFORMATION_SCHEMA.ROUTINES r
            WHERE r.ROUTINE_TYPE = 'FUNCTION'
            """;

        if (selectedFunctions != null && selectedFunctions.Count > 0)
        {
            sql += " AND (r.ROUTINE_SCHEMA + '.' + r.ROUTINE_NAME) IN @SelectedFunctions ";
        }

        sql += " ORDER BY r.ROUTINE_SCHEMA, r.ROUTINE_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedFunctions = selectedFunctions }, cancellationToken);

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
        IReadOnlyCollection<string>? selectedTriggers = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                tr.name             AS Name,
                s.name              AS SchemaName,
                t.name              AS TableName,
                te.type_desc        AS TriggerEvent,
                CASE tr.is_instead_of_trigger
                    WHEN 1 THEN 'INSTEAD OF'
                    ELSE 'AFTER'
                END                 AS ActionTiming,
                tr.is_disabled      AS IsDisabled,
                m.definition        AS Definition,
                tr.create_date      AS CreatedAt,
                tr.modify_date      AS ModifiedAt
            FROM sys.triggers tr
            JOIN sys.tables t       ON t.object_id = tr.parent_id
            JOIN sys.schemas s      ON s.schema_id = t.schema_id
            JOIN sys.trigger_events te ON te.object_id = tr.object_id
            JOIN sys.sql_modules m  ON m.object_id = tr.object_id
            WHERE tr.is_ms_shipped = 0
            """;

        if (selectedTriggers != null && selectedTriggers.Count > 0)
        {
            sql += " AND (s.name + '.' + tr.name) IN @SelectedTriggers ";
        }

        sql += " ORDER BY s.name, tr.name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTriggers = selectedTriggers }, cancellationToken);

        return rows.Select(r => new TriggerSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            TableName = r.TableName,
            TriggerEvent = r.TriggerEvent,
            ActionTiming = r.ActionTiming,
            IsEnabled = !(bool)r.IsDisabled,
            Definition = r.Definition,
            NormalizedDefinition = NormalizeDefinition(r.Definition),
            CreatedAt = r.CreatedAt,
            ModifiedAt = r.ModifiedAt
        }).ToList();
    }

    public override async Task<List<ConstraintSchema>> GetConstraintsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                tc.CONSTRAINT_NAME      AS Name,
                tc.TABLE_SCHEMA         AS SchemaName,
                tc.TABLE_NAME           AS TableName,
                tc.CONSTRAINT_TYPE      AS ConstraintType,
                kcu.COLUMN_NAME         AS ColumnName,
                ccu.TABLE_NAME          AS ReferencedTable,
                ccu.COLUMN_NAME         AS ReferencedColumn,
                cc.CHECK_CLAUSE         AS CheckClause
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                AND kcu.TABLE_SCHEMA = tc.TABLE_SCHEMA
            LEFT JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
                ON rc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
            LEFT JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu
                ON ccu.CONSTRAINT_NAME = rc.UNIQUE_CONSTRAINT_NAME
            LEFT JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
                ON cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
            WHERE 1=1
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (tc.TABLE_SCHEMA + '.' + tc.TABLE_NAME) IN @SelectedTables ";
        }

        sql += " ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, tc.CONSTRAINT_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        var grouped = rows.GroupBy(r => new { Name = (string)r.Name, SchemaName = (string)r.SchemaName, TableName = (string)r.TableName });

        return grouped.Select(g =>
        {
            var first = g.First();
            var columnNames = string.Join(",", g.Select(x => (string)x.ColumnName).Where(c => !string.IsNullOrEmpty(c)));
            var referencedColumns = string.Join(",", g.Select(x => (string)x.ReferencedColumn).Where(c => !string.IsNullOrEmpty(c)));

            return new ConstraintSchema
            {
                Name = first.Name,
                SchemaName = first.SchemaName,
                TableName = first.TableName,
                ConstraintType = first.ConstraintType,
                ColumnName = string.IsNullOrEmpty(columnNames) ? null : columnNames,
                ReferencedTable = first.ReferencedTable,
                ReferencedColumn = string.IsNullOrEmpty(referencedColumns) ? null : referencedColumns,
                CheckClause = first.CheckClause
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
                i.name              AS Name,
                s.name              AS SchemaName,
                t.name              AS TableName,
                i.type_desc         AS IndexType,
                i.is_unique         AS IsUnique,
                i.is_primary_key    AS IsPrimaryKey,
                i.is_disabled       AS IsDisabled,
                CASE i.index_id WHEN 1 THEN 1 ELSE 0 END AS IsClustered,
                STUFF((
                    SELECT ',' + col.name
                    FROM sys.index_columns ic
                    JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    ORDER BY ic.key_ordinal
                    FOR XML PATH('')), 1, 1, '') AS Columns
            FROM sys.indexes i
            JOIN sys.tables t       ON t.object_id = i.object_id
            JOIN sys.schemas s      ON s.schema_id = t.schema_id
            WHERE i.name IS NOT NULL
              AND t.is_ms_shipped = 0
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (s.name + '.' + t.name) IN @SelectedTables ";
        }

        sql += " ORDER BY s.name, t.name, i.name";

        var rows = await QueryAsync<dynamic>(connection, sql, new { SelectedTables = selectedTables }, cancellationToken);

        return rows.Select(r => new IndexSchema
        {
            Name = r.Name,
            SchemaName = r.SchemaName,
            TableName = r.TableName,
            IndexType = r.IndexType,
            IsUnique = (bool)r.IsUnique,
            IsPrimaryKey = (bool)r.IsPrimaryKey,
            IsDisabled = (bool)r.IsDisabled,
            IsClustered = r.IsClustered == 1,
            Columns = (r.Columns as string)?.Split(',').ToList() ?? new List<string>()
        }).ToList();
    }
}