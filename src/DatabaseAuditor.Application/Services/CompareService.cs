using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Domain.ValueObjects;
using Serilog;

namespace DatabaseAuditor.Application.Services;

public class CompareService : ICompareService
{
    private readonly IDatabaseProviderResolver _providerResolver;

    public CompareService(IDatabaseProviderResolver providerResolver)
    {
        _providerResolver = providerResolver;
    }

    public async Task<CompareSession> CompareAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CompareType compareType,
        ComparisonScope comparisonScope,
        IReadOnlyCollection<string>? selectedObjects = null,
        CancellationToken cancellationToken = default)
    {
        var session = new CompareSession
        {
            Source = source,
            Target = target,
            CompareType = compareType,
            ComparisonScope = comparisonScope,
            SelectedObjects = selectedObjects?.ToList() ?? [],
            StartedAt = DateTime.UtcNow
        };

        var sourceProvider = _providerResolver.GetProvider(source.DatabaseType);
        var targetProvider = _providerResolver.GetProvider(target.DatabaseType);
        var selectedSet = CreateSelectedSet(selectedObjects);

        try
        {
            session.Results = compareType switch
            {
                CompareType.Table => await CompareTablesAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Column => await CompareColumnsAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Procedure => await CompareProceduresAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.View => await CompareViewsAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Function => await CompareFunctionsAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Trigger => await CompareTriggersAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Constraint => await CompareConstraintsAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Index => await CompareIndexesAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Database => await CompareDatabaseAsync(sourceProvider, targetProvider, source, target, cancellationToken),
                _ => throw new NotSupportedException($"CompareType '{compareType}' is not supported.")
            };

            Log.Information("CompareSession Results Count: {Count}", session.Results.Count);
            session.IsSuccess = true;
        }
        catch (Exception ex)
        {
            session.IsSuccess = false;
            session.ErrorMessage = ex.Message;
            Log.Error(ex, "[CompareService] Comparison failed");
        }
        finally
        {
            session.CompletedAt = DateTime.UtcNow;
        }

        Log.Information("[CompareService] Returning session. Success={Success} Total={Total} Added={Added} Deleted={Deleted} Modified={Modified} Unchanged={Unchanged}",
            session.IsSuccess,
            session.Results.Count,
            session.Results.Count(r => r.ChangeType == ChangeType.Added),
            session.Results.Count(r => r.ChangeType == ChangeType.Deleted),
            session.Results.Count(r => r.ChangeType == ChangeType.Modified),
            session.Results.Count(r => r.ChangeType == ChangeType.Unchanged));

        return session;
    }

    private async Task<List<CompareResult>> CompareTablesAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var sourceTask = LoadDetailedTablesAsync(sourceProvider, source, selectedList, cancellationToken);
        var targetTask = LoadDetailedTablesAsync(targetProvider, target, selectedList, cancellationToken);

        await Task.WhenAll(sourceTask, targetTask);

        var sourceTables = sourceTask.Result;
        var targetTables = targetTask.Result;

        var results = new List<CompareResult>();
        var sourceMap = ToSafeDictionary(sourceTables, t => t.FullName, t => t, StringComparer.OrdinalIgnoreCase);
        var targetMap = ToSafeDictionary(targetTables, t => t.FullName, t => t, StringComparer.OrdinalIgnoreCase);
        var keys = sourceMap.Keys
            .Union(targetMap.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var hasSource = sourceMap.TryGetValue(key, out var sourceTable);
            var hasTarget = targetMap.TryGetValue(key, out var targetTable);

            if (hasSource && !hasTarget)
            {
                results.Add(CreateMissingResult(sourceTable!, CompareType.Table, ChangeType.Deleted, ObjectStatus.MissingInTarget, key, null));
                continue;
            }

            if (!hasSource && hasTarget)
            {
                results.Add(CreateMissingResult(targetTable!, CompareType.Table, ChangeType.Added, ObjectStatus.MissingInSource, null, key));
                continue;
            }

            var sourceTableValue = sourceTable!;
            var targetTableValue = targetTable!;

            var tableDiffs = GetTableDifferences(sourceTableValue, targetTableValue);
            results.Add(CreateCompareResult(sourceTableValue, CompareType.Table, tableDiffs, key, key, parentObject: null));

            results.AddRange(CompareChildObjects(
                sourceTableValue.Columns,
                targetTableValue.Columns,
                c => $"{c.TableName}.{c.FullName}",
                CompareType.Column,
                (s, t) => GetColumnDifferences(s, t),
                sourceTableValue.FullName));

            results.AddRange(CompareChildObjects(
                sourceTableValue.Constraints,
                targetTableValue.Constraints,
                c => $"{c.TableName}.{c.FullName}",
                CompareType.Constraint,
                (s, t) => GetConstraintDifferences(s, t),
                sourceTableValue.FullName));

            results.AddRange(CompareChildObjects(
                sourceTableValue.Indexes,
                targetTableValue.Indexes,
                i => $"{i.TableName}.{i.FullName}",
                CompareType.Index,
                (s, t) => GetIndexDifferences(s, t),
                sourceTableValue.FullName));
        }

        return results;
    }

    private async Task<List<CompareResult>> CompareColumnsAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceColumns, targetColumns) = await FetchBothAsync(
            () => sourceProvider.GetColumnsAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetColumnsAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceColumns,
            targetColumns,
            c => $"{c.TableName}.{c.FullName}",
            CompareType.Column,
            (s, t) => GetColumnDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareProceduresAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceProcs, targetProcs) = await FetchBothAsync(
            () => sourceProvider.GetProceduresAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetProceduresAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceProcs,
            targetProcs,
            p => p.FullName,
            CompareType.Procedure,
            (s, t) => GetProcedureDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareViewsAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceViews, targetViews) = await FetchBothAsync(
            () => sourceProvider.GetViewsAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetViewsAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceViews,
            targetViews,
            v => v.FullName,
            CompareType.View,
            (s, t) => GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
    }

    private async Task<List<CompareResult>> CompareFunctionsAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceFuncs, targetFuncs) = await FetchBothAsync(
            () => sourceProvider.GetFunctionsAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetFunctionsAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceFuncs,
            targetFuncs,
            f => f.FullName,
            CompareType.Function,
            (s, t) => GetFunctionDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareTriggersAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceTriggers, targetTriggers) = await FetchBothAsync(
            () => sourceProvider.GetTriggersAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetTriggersAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceTriggers,
            targetTriggers,
            t => t.FullName,
            CompareType.Trigger,
            (s, t) => GetTriggerDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareConstraintsAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceConstraints, targetConstraints) = await FetchBothAsync(
            () => sourceProvider.GetConstraintsAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetConstraintsAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceConstraints,
            targetConstraints,
            c => $"{c.TableName}.{c.FullName}",
            CompareType.Constraint,
            (s, t) => GetConstraintDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareIndexesAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var selectedList = selectedObjects.Count > 0 ? selectedObjects.ToList() : null;
        var (sourceIndexes, targetIndexes) = await FetchBothAsync(
            () => sourceProvider.GetIndexesAsync(source, selectedList, cancellationToken),
            () => targetProvider.GetIndexesAsync(target, selectedList, cancellationToken));

        return CompareObjectLists(
            sourceIndexes,
            targetIndexes,
            i => $"{i.TableName}.{i.FullName}",
            CompareType.Index,
            (s, t) => GetIndexDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareDatabaseAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var tasks = new[]
        {
            CompareTablesAsync(sourceProvider, targetProvider, source, target, [], cancellationToken),
            CompareProceduresAsync(sourceProvider, targetProvider, source, target, [], cancellationToken),
            CompareViewsAsync(sourceProvider, targetProvider, source, target, [], cancellationToken),
            CompareFunctionsAsync(sourceProvider, targetProvider, source, target, [], cancellationToken),
            CompareTriggersAsync(sourceProvider, targetProvider, source, target, [], cancellationToken)
        };

        var groups = await Task.WhenAll(tasks);
        return groups.SelectMany(g => g).ToList();
    }

    private static async Task<List<TableSchema>> LoadDetailedTablesAsync(
        IDatabaseProvider provider,
        ConnectionProfile connection,
        IReadOnlyCollection<string>? selectedTables,
        CancellationToken cancellationToken)
    {
        var tablesTask = provider.GetTablesAsync(connection, selectedTables, cancellationToken);
        var columnsTask = provider.GetColumnsAsync(connection, selectedTables, cancellationToken);
        var constraintsTask = provider.GetConstraintsAsync(connection, selectedTables, cancellationToken);
        var indexesTask = provider.GetIndexesAsync(connection, selectedTables, cancellationToken);

        await Task.WhenAll(tablesTask, columnsTask, constraintsTask, indexesTask);

        var tableMap = ToSafeDictionary(tablesTask.Result, t => t.FullName, t => t, StringComparer.OrdinalIgnoreCase);

        foreach (var column in columnsTask.Result)
        {
            var key = $"{column.SchemaName}.{column.TableName}";
            if (tableMap.TryGetValue(key, out var table))
                table.Columns.Add(column);
        }

        foreach (var constraint in constraintsTask.Result)
        {
            var key = $"{constraint.SchemaName}.{constraint.TableName}";
            if (tableMap.TryGetValue(key, out var table))
                table.Constraints.Add(constraint);
        }

        foreach (var index in indexesTask.Result)
        {
            var key = $"{index.SchemaName}.{index.TableName}";
            if (tableMap.TryGetValue(key, out var table))
                table.Indexes.Add(index);
        }

        return tableMap.Values.ToList();
    }

    private static HashSet<string> CreateSelectedSet(IReadOnlyCollection<string>? selectedObjects)
        => selectedObjects == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                selectedObjects
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);

    private static async Task<(List<T> Source, List<T> Target)> FetchBothAsync<T>(
        Func<Task<List<T>>> sourceFunc,
        Func<Task<List<T>>> targetFunc)
    {
        var sourceTask = sourceFunc();
        var targetTask = targetFunc();
        await Task.WhenAll(sourceTask, targetTask);
        return (sourceTask.Result, targetTask.Result);
    }

    private static List<CompareResult> CompareChildObjects<T>(
        List<T> sourceList,
        List<T> targetList,
        Func<T, string> keySelector,
        CompareType objectType,
        Func<T, T, List<string>> diffSelector,
        string parentObject) where T : SchemaObject
    {
        var results = CompareObjectLists(sourceList, targetList, keySelector, objectType, diffSelector);
        foreach (var result in results)
            result.ParentObject = parentObject;
        return results;
    }

    private static Dictionary<string, TElement> ToSafeDictionary<TSource, TElement>(
        IEnumerable<TSource> source,
        Func<TSource, string> keySelector,
        Func<TSource, TElement> elementSelector,
        IEqualityComparer<string> comparer)
    {
        var dictionary = new Dictionary<string, TElement>(comparer);
        foreach (var item in source)
        {
            var key = keySelector(item);
            if (string.IsNullOrEmpty(key)) continue;
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key, elementSelector(item));
            }
        }
        return dictionary;
    }

    private static List<CompareResult> CompareObjectLists<T>(
        List<T> sourceList,
        List<T> targetList,
        Func<T, string> keySelector,
        CompareType objectType,
        Func<T, T, List<string>> diffSelector) where T : SchemaObject
    {
        var results = new List<CompareResult>();
        var sourceMap = ToSafeDictionary(sourceList, keySelector, x => x, StringComparer.OrdinalIgnoreCase);
        var targetMap = ToSafeDictionary(targetList, keySelector, x => x, StringComparer.OrdinalIgnoreCase);
        var allKeys = sourceMap.Keys.Union(targetMap.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var key in allKeys)
        {
            var inSource = sourceMap.TryGetValue(key, out var sourceObj);
            var inTarget = targetMap.TryGetValue(key, out var targetObj);

            if (inSource && !inTarget)
            {
                results.Add(CreateMissingResult(sourceObj!, objectType, ChangeType.Deleted, ObjectStatus.MissingInTarget, key, null));
                continue;
            }

            if (!inSource && inTarget)
            {
                results.Add(CreateMissingResult(targetObj!, objectType, ChangeType.Added, ObjectStatus.MissingInSource, null, key));
                continue;
            }

            var diffs = diffSelector(sourceObj!, targetObj!);
            results.Add(CreateCompareResult(sourceObj!, objectType, diffs, key, key, null));
        }

        return results;
    }

    private static CompareResult CreateMissingResult(
        SchemaObject schemaObject,
        CompareType objectType,
        ChangeType changeType,
        ObjectStatus status,
        string? sourceValue,
        string? targetValue)
        => new ()
        {
            ObjectType = objectType,
            ObjectName = schemaObject.Name,
            SchemaName = schemaObject.SchemaName,
            ChangeType = changeType,
            Status = status,
            SourceValue = sourceValue,
            TargetValue = targetValue
        };

    private static CompareResult CreateCompareResult(
        SchemaObject schemaObject,
        CompareType objectType,
        List<string> differences,
        string? sourceValue,
        string? targetValue,
        string? parentObject)
        => new ()
        {
            ObjectType = objectType,
            ObjectName = schemaObject.Name,
            SchemaName = schemaObject.SchemaName,
            ChangeType = differences.Count > 0 ? ChangeType.Modified : ChangeType.Unchanged,
            Status = differences.Count > 0 ? ObjectStatus.Mismatch : ObjectStatus.Match,
            SourceValue = sourceValue,
            TargetValue = targetValue,
            Differences = differences,
            ParentObject = parentObject
        };

    private static List<string> GetTableDifferences(TableSchema s, TableSchema t)
    {
        var diffs = new List<string>();
        if (!string.Equals(s.Engine, t.Engine, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"Engine: {s.Engine ?? "NULL"} -> {t.Engine ?? "NULL"}");
        if (!string.Equals(s.Collation, t.Collation, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"Collation: {s.Collation ?? "NULL"} -> {t.Collation ?? "NULL"}");
        if (s.TotalRows != t.TotalRows)
            diffs.Add($"Approximate Rows: {s.TotalRows} -> {t.TotalRows}");
        return diffs;
    }

    private static List<string> GetColumnDifferences(ColumnSchema s, ColumnSchema t)
    {
        var diffs = new List<string>();
        if (!string.Equals(s.DataType, t.DataType, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"DataType: {s.DataType} -> {t.DataType}");
        if (s.MaxLength != t.MaxLength)
            diffs.Add($"MaxLength: {s.MaxLength?.ToString() ?? "NULL"} -> {t.MaxLength?.ToString() ?? "NULL"}");
        if (s.Precision != t.Precision)
            diffs.Add($"Precision: {s.Precision?.ToString() ?? "NULL"} -> {t.Precision?.ToString() ?? "NULL"}");
        if (s.Scale != t.Scale)
            diffs.Add($"Scale: {s.Scale?.ToString() ?? "NULL"} -> {t.Scale?.ToString() ?? "NULL"}");
        if (s.IsNullable != t.IsNullable)
            diffs.Add($"Nullable: {s.IsNullable} -> {t.IsNullable}");
        if (s.IsIdentity != t.IsIdentity)
            diffs.Add($"Identity: {s.IsIdentity} -> {t.IsIdentity}");
        if (s.IsPrimaryKey != t.IsPrimaryKey)
            diffs.Add($"PrimaryKey: {s.IsPrimaryKey} -> {t.IsPrimaryKey}");
        if (s.IsForeignKey != t.IsForeignKey)
            diffs.Add($"ForeignKey: {s.IsForeignKey} -> {t.IsForeignKey}");
        if (s.IsComputed != t.IsComputed)
            diffs.Add($"Computed: {s.IsComputed} -> {t.IsComputed}");

        var sDefault = s.DefaultValue?.Trim() ?? string.Empty;
        var tDefault = t.DefaultValue?.Trim() ?? string.Empty;
        if (!string.Equals(sDefault, tDefault, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"Default: {(string.IsNullOrEmpty(sDefault) ? "NULL" : sDefault)} -> {(string.IsNullOrEmpty(tDefault) ? "NULL" : tDefault)}");

        if (s.OrdinalPosition != t.OrdinalPosition)
            diffs.Add($"Position: {s.OrdinalPosition} -> {t.OrdinalPosition}");
        return diffs;
    }

    private static List<string> GetProcedureDifferences(ProcedureSchema s, ProcedureSchema t)
    {
        var diffs = GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition);
        if (!string.Equals(s.ReturnType, t.ReturnType, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"ReturnType: {s.ReturnType ?? "NONE"} -> {t.ReturnType ?? "NONE"}");
        if (!string.Equals(s.SchemaName, t.SchemaName, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"Schema: {s.SchemaName} -> {t.SchemaName}");

        // Compare parameters count and details
        if (s.Parameters.Count != t.Parameters.Count)
        {
            diffs.Add($"Parameter Count: {s.Parameters.Count} -> {t.Parameters.Count}");
        }

        var maxCount = Math.Max(s.Parameters.Count, t.Parameters.Count);
        for (int i = 0; i < maxCount; i++)
        {
            if (i < s.Parameters.Count && i < t.Parameters.Count)
            {
                var sp = s.Parameters[i];
                var tp = t.Parameters[i];

                if (!string.Equals(sp.Name, tp.Name, StringComparison.OrdinalIgnoreCase))
                {
                    diffs.Add($"Parameter {i + 1} Name: {sp.Name} -> {tp.Name}");
                }
                if (!string.Equals(sp.DataType, tp.DataType, StringComparison.OrdinalIgnoreCase))
                {
                    diffs.Add($"Parameter {i + 1} '{sp.Name}' DataType: {sp.DataType} -> {tp.DataType}");
                }
                if (sp.IsOutput != tp.IsOutput)
                {
                    diffs.Add($"Parameter {i + 1} '{sp.Name}' IsOutput: {sp.IsOutput} -> {tp.IsOutput}");
                }
                if (sp.OrdinalPosition != tp.OrdinalPosition)
                {
                    diffs.Add($"Parameter {i + 1} '{sp.Name}' Position: {sp.OrdinalPosition} -> {tp.OrdinalPosition}");
                }
            }
            else if (i < s.Parameters.Count)
            {
                diffs.Add($"Parameter '{s.Parameters[i].Name}' is missing in Target");
            }
            else
            {
                diffs.Add($"Parameter '{t.Parameters[i].Name}' is missing in Source");
            }
        }

        return diffs;
    }

    private static List<string> GetDefinitionDifferences(string? s, string? t)
    {
        var diffs = new List<string>();
        var normalizedSource = s?.Trim().ToUpperInvariant();
        var normalizedTarget = t?.Trim().ToUpperInvariant();
        if (normalizedSource != normalizedTarget)
            diffs.Add("Definition has changed.");
        return diffs;
    }

    private static List<string> GetFunctionDifferences(FunctionSchema s, FunctionSchema t)
    {
        var diffs = GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition);
        if (!string.Equals(s.ReturnType, t.ReturnType, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"ReturnType: {s.ReturnType ?? "NONE"} -> {t.ReturnType ?? "NONE"}");
        return diffs;
    }

    private static List<string> GetTriggerDifferences(TriggerSchema s, TriggerSchema t)
    {
        var diffs = new List<string>();
        if (s.TriggerEvent != t.TriggerEvent) diffs.Add($"Event: {s.TriggerEvent} -> {t.TriggerEvent}");
        if (s.ActionTiming != t.ActionTiming) diffs.Add($"Timing: {s.ActionTiming} -> {t.ActionTiming}");
        if (s.IsEnabled != t.IsEnabled) diffs.Add($"Enabled: {s.IsEnabled} -> {t.IsEnabled}");
        diffs.AddRange(GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
        return diffs;
    }

    private static List<string> GetConstraintDifferences(ConstraintSchema s, ConstraintSchema t)
    {
        var diffs = new List<string>();
        if (s.ConstraintType != t.ConstraintType) diffs.Add($"Type: {s.ConstraintType} -> {t.ConstraintType}");
        if (s.ColumnName != t.ColumnName) diffs.Add($"Column: {s.ColumnName} -> {t.ColumnName}");
        if (s.ReferencedTable != t.ReferencedTable) diffs.Add($"ReferencedTable: {s.ReferencedTable} -> {t.ReferencedTable}");
        if (s.ReferencedColumn != t.ReferencedColumn) diffs.Add($"ReferencedColumn: {s.ReferencedColumn} -> {t.ReferencedColumn}");
        if (s.CheckClause != t.CheckClause) diffs.Add($"CheckClause: {s.CheckClause} -> {t.CheckClause}");
        return diffs;
    }

    private static List<string> GetIndexDifferences(IndexSchema s, IndexSchema t)
    {
        var diffs = new List<string>();
        if (s.IndexType != t.IndexType) diffs.Add($"Type: {s.IndexType} -> {t.IndexType}");
        if (s.IsUnique != t.IsUnique) diffs.Add($"Unique: {s.IsUnique} -> {t.IsUnique}");
        if (s.IsClustered != t.IsClustered) diffs.Add($"Clustered: {s.IsClustered} -> {t.IsClustered}");
        if (s.IsPrimaryKey != t.IsPrimaryKey) diffs.Add($"PrimaryKey: {s.IsPrimaryKey} -> {t.IsPrimaryKey}");
        if (s.IsDisabled != t.IsDisabled) diffs.Add($"Disabled: {s.IsDisabled} -> {t.IsDisabled}");
        var sourceCols = string.Join(",", s.Columns.OrderBy(c => c));
        var targetCols = string.Join(",", t.Columns.OrderBy(c => c));
        if (sourceCols != targetCols) diffs.Add($"Columns: [{sourceCols}] -> [{targetCols}]");
        return diffs;
    }
}
