using Dapper;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Providers.Base;
using global::Oracle.ManagedDataAccess.Client;
using System.Data;

namespace DatabaseAuditor.Providers.Oracle;

public class OracleProvider : BaseDatabaseProvider
{
    protected override IDbConnection CreateConnection(ConnectionProfile connection)
    {
        var connectionString =
            $"Data Source={connection.Server}:{connection.Port}/{connection.DatabaseName};" +
            $"User Id={connection.Username};" +
            $"Password={connection.Password};" +
            $"Connection Timeout=30;";

        return new OracleConnection(connectionString);
    }

    public override async Task<bool> TestConnectionAsync(
        ConnectionProfile connection,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = CreateConnection(connection);
            await ((OracleConnection)conn).OpenAsync(cancellationToken);
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
                t.OWNER             AS SchemaName,
                t.NUM_ROWS          AS TotalRows
            FROM ALL_TABLES t
            WHERE t.OWNER = UPPER(:SchemaName)
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (t.OWNER || '.' || t.TABLE_NAME) IN :SelectedTables";
        }

        sql += " ORDER BY t.OWNER, t.TABLE_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedTables = selectedTables },
            cancellationToken);

        return rows.Select(r => new TableSchema
        {
            Name = r.NAME,
            SchemaName = r.SCHEMANAME,
            TotalRows = r.TOTALROWS ?? 0
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
                c.OWNER                 AS SchemaName,
                c.TABLE_NAME            AS TableName,
                c.COLUMN_ID             AS OrdinalPosition,
                c.DATA_TYPE             AS DataType,
                c.CHAR_LENGTH           AS MaxLength,
                c.DATA_PRECISION        AS Precision,
                c.DATA_SCALE            AS Scale,
                CASE WHEN c.NULLABLE = 'Y' THEN 1 ELSE 0 END AS IsNullable,
                CASE WHEN ic.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsIdentity,
                c.DATA_DEFAULT          AS DefaultValue,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey
            FROM ALL_TAB_COLUMNS c
            LEFT JOIN ALL_IDENTITY_COLUMNS ic
                ON ic.OWNER = c.OWNER
                AND ic.TABLE_NAME = c.TABLE_NAME
                AND ic.COLUMN_NAME = c.COLUMN_NAME
            LEFT JOIN (
                SELECT ac.OWNER, ac.TABLE_NAME, acc.COLUMN_NAME
                FROM ALL_CONSTRAINTS ac
                JOIN ALL_CONS_COLUMNS acc
                    ON acc.CONSTRAINT_NAME = ac.CONSTRAINT_NAME
                WHERE ac.CONSTRAINT_TYPE = 'P'
            ) pk ON pk.OWNER = c.OWNER
                AND pk.TABLE_NAME = c.TABLE_NAME
                AND pk.COLUMN_NAME = c.COLUMN_NAME
            LEFT JOIN (
                SELECT ac.OWNER, ac.TABLE_NAME, acc.COLUMN_NAME
                FROM ALL_CONSTRAINTS ac
                JOIN ALL_CONS_COLUMNS acc
                    ON acc.CONSTRAINT_NAME = ac.CONSTRAINT_NAME
                WHERE ac.CONSTRAINT_TYPE = 'R'
            ) fk ON fk.OWNER = c.OWNER
                AND fk.TABLE_NAME = c.TABLE_NAME
                AND fk.COLUMN_NAME = c.COLUMN_NAME
            WHERE c.OWNER = UPPER(:SchemaName)
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (c.OWNER || '.' || c.TABLE_NAME) IN :SelectedTables";
        }

        sql += " ORDER BY c.OWNER, c.TABLE_NAME, c.COLUMN_ID";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedTables = selectedTables },
            cancellationToken);

        return rows.Select(r => new ColumnSchema
        {
            Name = r.NAME,
            SchemaName = r.SCHEMANAME,
            TableName = r.TABLENAME,
            OrdinalPosition = r.ORDINALPOSITION,
            DataType = r.DATATYPE,
            MaxLength = r.MAXLENGTH,
            Precision = r.PRECISION,
            Scale = r.SCALE,
            IsNullable = r.ISNULLABLE == 1,
            IsIdentity = r.ISIDENTITY == 1,
            DefaultValue = r.DEFAULTVALUE,
            IsPrimaryKey = r.ISPRIMARYKEY == 1,
            IsForeignKey = r.ISFOREIGNKEY == 1
        }).ToList();
    }

    public override async Task<List<ProcedureSchema>> GetProceduresAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedProcedures = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                p.OBJECT_NAME       AS Name,
                p.OWNER             AS SchemaName,
                s.TEXT              AS Definition,
                p.CREATED           AS CreatedAt,
                p.LAST_DDL_TIME     AS ModifiedAt
            FROM ALL_OBJECTS p
            JOIN ALL_SOURCE s
                ON s.OWNER = p.OWNER
                AND s.NAME = p.OBJECT_NAME
                AND s.TYPE = 'PROCEDURE'
            WHERE p.OBJECT_TYPE = 'PROCEDURE'
              AND p.OWNER = UPPER(:SchemaName)
            """;

        if (selectedProcedures != null && selectedProcedures.Count > 0)
        {
            sql += " AND (p.OWNER || '.' || p.OBJECT_NAME) IN :SelectedProcedures";
        }

        sql += " ORDER BY p.OWNER, p.OBJECT_NAME, s.LINE";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedProcedures = selectedProcedures },
            cancellationToken);

        // Aggregate multi-line source
        var grouped = rows
            .GroupBy(r => new { Name = (string)r.NAME, SchemaName = (string)r.SCHEMANAME })
            .Select(g => new ProcedureSchema
            {
                Name = g.Key.Name,
                SchemaName = g.Key.SchemaName,
                Definition = string.Concat(g.Select(r => (string)r.DEFINITION)),
                CreatedAt = g.First().CREATEDAT,
                ModifiedAt = g.First().MODIFIEDAT
            })
            .ToList();

        foreach (var p in grouped)
            p.NormalizedDefinition = NormalizeDefinition(p.Definition);

        // Load parameters
        var paramSql = """
            SELECT
                OBJECT_NAME AS RoutineName,
                OWNER AS RoutineSchema,
                ARGUMENT_NAME AS Name,
                DATA_TYPE AS DataType,
                POSITION AS OrdinalPosition,
                IN_OUT AS ParameterMode
            FROM ALL_ARGUMENTS
            WHERE OWNER = UPPER(:SchemaName)
              AND ARGUMENT_NAME IS NOT NULL
            """;

        if (selectedProcedures != null && selectedProcedures.Count > 0)
        {
            paramSql += " AND (OWNER || '.' || OBJECT_NAME) IN :SelectedProcedures";
        }

        var paramRows = await QueryAsync<dynamic>(connection, paramSql,
            new { SchemaName = connection.Username.ToUpper(), SelectedProcedures = selectedProcedures },
            cancellationToken);

        var paramMap = paramRows.GroupBy(p => $"{p.ROUTINESCHEMA}.{p.ROUTINENAME}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var proc in grouped)
        {
            if (paramMap.TryGetValue(proc.FullName, out var paramsList))
            {
                proc.Parameters = paramsList.Select(p => new ParameterSchema
                {
                    Name = p.NAME ?? string.Empty,
                    DataType = (string)p.DATATYPE,
                    OrdinalPosition = Convert.ToInt32(p.ORDINALPOSITION),
                    IsOutput = p.PARAMETERMODE == "OUT" || p.PARAMETERMODE == "IN/OUT"
                }).OrderBy(p => p.OrdinalPosition).ToList();
            }
        }

        return grouped;
    }

    public override async Task<List<ViewSchema>> GetViewsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedViews = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                v.VIEW_NAME         AS Name,
                v.OWNER             AS SchemaName,
                v.TEXT              AS Definition
            FROM ALL_VIEWS v
            WHERE v.OWNER = UPPER(:SchemaName)
            """;

        if (selectedViews != null && selectedViews.Count > 0)
        {
            sql += " AND (v.OWNER || '.' || v.VIEW_NAME) IN :SelectedViews";
        }

        sql += " ORDER BY v.OWNER, v.VIEW_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedViews = selectedViews },
            cancellationToken);

        return rows.Select(r => new ViewSchema
        {
            Name = r.NAME,
            SchemaName = r.SCHEMANAME,
            Definition = r.DEFINITION,
            NormalizedDefinition = NormalizeDefinition(r.DEFINITION)
        }).ToList();
    }

    public override async Task<List<FunctionSchema>> GetFunctionsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedFunctions = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                o.OBJECT_NAME       AS Name,
                o.OWNER             AS SchemaName,
                s.TEXT              AS Definition,
                o.CREATED           AS CreatedAt,
                o.LAST_DDL_TIME     AS ModifiedAt
            FROM ALL_OBJECTS o
            JOIN ALL_SOURCE s
                ON s.OWNER = o.OWNER
                AND s.NAME = o.OBJECT_NAME
                AND s.TYPE = 'FUNCTION'
            WHERE o.OBJECT_TYPE = 'FUNCTION'
              AND o.OWNER = UPPER(:SchemaName)
            """;

        if (selectedFunctions != null && selectedFunctions.Count > 0)
        {
            sql += " AND (o.OWNER || '.' || o.OBJECT_NAME) IN :SelectedFunctions";
        }

        sql += " ORDER BY o.OWNER, o.OBJECT_NAME, s.LINE";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedFunctions = selectedFunctions },
            cancellationToken);

        var grouped = rows
            .GroupBy(r => new { Name = (string)r.NAME, SchemaName = (string)r.SCHEMANAME })
            .Select(g => new FunctionSchema
            {
                Name = g.Key.Name,
                SchemaName = g.Key.SchemaName,
                Definition = string.Concat(g.Select(r => (string)r.DEFINITION)),
                FunctionType = "FUNCTION",
                CreatedAt = g.First().CREATEDAT,
                ModifiedAt = g.First().MODIFIEDAT
            })
            .ToList();

        foreach (var f in grouped)
            f.NormalizedDefinition = NormalizeDefinition(f.Definition);

        return grouped;
    }

    public override async Task<List<TriggerSchema>> GetTriggersAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTriggers = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                t.TRIGGER_NAME      AS Name,
                t.OWNER             AS SchemaName,
                t.TABLE_NAME        AS TableName,
                t.TRIGGERING_EVENT  AS TriggerEvent,
                t.TRIGGER_TYPE      AS ActionTiming,
                t.STATUS            AS Status,
                t.TRIGGER_BODY      AS Definition,
                o.CREATED           AS CreatedAt,
                o.LAST_DDL_TIME     AS ModifiedAt
            FROM ALL_TRIGGERS t
            JOIN ALL_OBJECTS o
                ON o.OWNER = t.OWNER
                AND o.OBJECT_NAME = t.TRIGGER_NAME
                AND o.OBJECT_TYPE = 'TRIGGER'
            WHERE t.OWNER = UPPER(:SchemaName)
            """;

        if (selectedTriggers != null && selectedTriggers.Count > 0)
        {
            sql += " AND (t.OWNER || '.' || t.TRIGGER_NAME) IN :SelectedTriggers";
        }

        sql += " ORDER BY t.OWNER, t.TRIGGER_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedTriggers = selectedTriggers },
            cancellationToken);

        return rows.Select(r => new TriggerSchema
        {
            Name = r.NAME,
            SchemaName = r.SCHEMANAME,
            TableName = r.TABLENAME,
            TriggerEvent = r.TRIGGEREVENT,
            ActionTiming = r.ACTIONTIMING,
            IsEnabled = r.STATUS == "ENABLED",
            Definition = r.DEFINITION,
            NormalizedDefinition = NormalizeDefinition(r.DEFINITION),
            CreatedAt = r.CREATEDAT,
            ModifiedAt = r.MODIFIEDAT
        }).ToList();
    }

    public override async Task<List<ConstraintSchema>> GetConstraintsAsync(
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables = null,
        CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT
                ac.CONSTRAINT_NAME      AS Name,
                ac.OWNER                AS SchemaName,
                ac.TABLE_NAME           AS TableName,
                ac.CONSTRAINT_TYPE      AS ConstraintType,
                acc.COLUMN_NAME         AS ColumnName,
                ac.R_OWNER              AS ReferencedSchema,
                rc.TABLE_NAME           AS ReferencedTable,
                rcc.COLUMN_NAME         AS ReferencedColumn,
                ac.SEARCH_CONDITION     AS CheckClause
            FROM ALL_CONSTRAINTS ac
            LEFT JOIN ALL_CONS_COLUMNS acc
                ON acc.CONSTRAINT_NAME = ac.CONSTRAINT_NAME
                AND acc.OWNER = ac.OWNER
            LEFT JOIN ALL_CONSTRAINTS rc
                ON rc.CONSTRAINT_NAME = ac.R_CONSTRAINT_NAME
                AND rc.OWNER = ac.R_OWNER
            LEFT JOIN ALL_CONS_COLUMNS rcc
                ON rcc.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
                AND rcc.OWNER = rc.OWNER
            WHERE ac.OWNER = UPPER(:SchemaName)
              AND ac.CONSTRAINT_TYPE IN ('P','R','U','C')
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (ac.OWNER || '.' || ac.TABLE_NAME) IN :SelectedTables";
        }

        sql += " ORDER BY ac.OWNER, ac.TABLE_NAME, ac.CONSTRAINT_NAME";

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedTables = selectedTables },
            cancellationToken);

        var grouped = rows.GroupBy(r => new { Name = (string)r.NAME, SchemaName = (string)r.SCHEMANAME, TableName = (string)r.TABLENAME });

        return grouped.Select(g =>
        {
            var first = g.First();
            var columnNames = string.Join(",", g.Select(x => (string)x.COLUMNNAME).Where(c => !string.IsNullOrEmpty(c)));
            var referencedColumns = string.Join(",", g.Select(x => (string)x.REFERENCEDCOLUMN).Where(c => !string.IsNullOrEmpty(c)));

            return new ConstraintSchema
            {
                Name = first.NAME,
                SchemaName = first.SCHEMANAME,
                TableName = first.TABLENAME,
                ConstraintType = first.CONSTRAINTTYPE switch
                {
                    "P" => "PRIMARY KEY",
                    "R" => "FOREIGN KEY",
                    "U" => "UNIQUE",
                    "C" => "CHECK",
                    _ => first.CONSTRAINTTYPE
                },
                ColumnName = string.IsNullOrEmpty(columnNames) ? null : columnNames,
                ReferencedTable = first.REFERENCEDTABLE,
                ReferencedColumn = string.IsNullOrEmpty(referencedColumns) ? null : referencedColumns,
                CheckClause = first.CHECKCLAUSE
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
                i.INDEX_NAME        AS Name,
                i.OWNER             AS SchemaName,
                i.TABLE_NAME        AS TableName,
                i.INDEX_TYPE        AS IndexType,
                CASE WHEN i.UNIQUENESS = 'UNIQUE' THEN 1 ELSE 0 END AS IsUnique,
                CASE WHEN c.CONSTRAINT_TYPE = 'P' THEN 1 ELSE 0 END AS IsPrimaryKey,
                CASE WHEN i.STATUS = 'UNUSABLE' THEN 1 ELSE 0 END AS IsDisabled,
                0                   AS IsClustered,
                LISTAGG(ic.COLUMN_NAME, ',')
                    WITHIN GROUP (ORDER BY ic.COLUMN_POSITION) AS Columns
            FROM ALL_INDEXES i
            JOIN ALL_IND_COLUMNS ic
                ON ic.INDEX_NAME = i.INDEX_NAME
                AND ic.INDEX_OWNER = i.OWNER
            LEFT JOIN ALL_CONSTRAINTS c
                ON c.INDEX_NAME = i.INDEX_NAME
                AND c.OWNER = i.OWNER
                AND c.CONSTRAINT_TYPE = 'P'
            WHERE i.OWNER = UPPER(:SchemaName)
            """;

        if (selectedTables != null && selectedTables.Count > 0)
        {
            sql += " AND (i.OWNER || '.' || i.TABLE_NAME) IN :SelectedTables";
        }

        sql += """
            GROUP BY i.INDEX_NAME, i.OWNER, i.TABLE_NAME,
                     i.INDEX_TYPE, i.UNIQUENESS,
                     c.CONSTRAINT_TYPE, i.STATUS
            ORDER BY i.OWNER, i.TABLE_NAME, i.INDEX_NAME
            """;

        var rows = await QueryAsync<dynamic>(connection, sql,
            new { SchemaName = connection.Username.ToUpper(), SelectedTables = selectedTables },
            cancellationToken);

        return rows.Select(r => new IndexSchema
        {
            Name = r.NAME,
            SchemaName = r.SCHEMANAME,
            TableName = r.TABLENAME,
            IndexType = r.INDEXTYPE,
            IsUnique = r.ISUNIQUE == 1,
            IsPrimaryKey = r.ISPRIMARYKEY == 1,
            IsDisabled = r.ISDISABLED == 1,
            IsClustered = false,
            Columns = ((string?)r.COLUMNS)?.Split(',').ToList() ?? []
        }).ToList();
    }
}