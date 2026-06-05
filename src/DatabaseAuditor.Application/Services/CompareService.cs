namespace DatabaseAuditor.Application.Services;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Domain.ValueObjects;
using Serilog;

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
                CompareType.Column => await CompareColumnsAsync(sourceProvider, targetProvider, source, target, cancellationToken),
                CompareType.Procedure => await CompareProceduresAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.View => await CompareViewsAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Function => await CompareFunctionsAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Trigger => await CompareTriggersAsync(sourceProvider, targetProvider, source, target, selectedSet, cancellationToken),
                CompareType.Constraint => await CompareConstraintsAsync(sourceProvider, targetProvider, source, target, cancellationToken),
                CompareType.Index => await CompareIndexesAsync(sourceProvider, targetProvider, source, target, cancellationToken),
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
        var sourceTask = LoadDetailedTablesAsync(sourceProvider, source, cancellationToken);
        var targetTask = LoadDetailedTablesAsync(targetProvider, target, cancellationToken);

        await Task.WhenAll(sourceTask, targetTask);

        var sourceTables = sourceTask.Result;
        var targetTables = targetTask.Result;

        if (selectedObjects.Count > 0)
        {
            sourceTables = sourceTables
                .Where(t => selectedObjects.Contains(t.FullName))
                .ToList();
            targetTables = targetTables
                .Where(t => selectedObjects.Contains(t.FullName))
                .ToList();
        }

        var results = new List<CompareResult>();
        var sourceMap = sourceTables.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
        var targetMap = targetTables.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);
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
        CancellationToken cancellationToken)
    {
        var (sourceColumns, targetColumns) = await FetchBothAsync(
            () => sourceProvider.GetColumnsAsync(source, cancellationToken),
            () => targetProvider.GetColumnsAsync(target, cancellationToken));

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
        var (sourceProcs, targetProcs) = await FetchBothAsync(
            () => targetProvider == sourceProvider
                ? sourceProvider.GetProceduresAsync(source, cancellationToken)
                : sourceProvider.GetProceduresAsync(source, cancellationToken),
            () => targetProvider.GetProceduresAsync(target, cancellationToken));

        var filteredSource = FilterBySelection(sourceProcs, selectedObjects);
        var filteredTarget = FilterBySelection(targetProcs, selectedObjects);

        return CompareObjectLists(
            filteredSource,
            filteredTarget,
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
        var (sourceViews, targetViews) = await FetchBothAsync(
            () => sourceProvider.GetViewsAsync(source, cancellationToken),
            () => targetProvider.GetViewsAsync(target, cancellationToken));

        return CompareObjectLists(
            FilterBySelection(sourceViews, selectedObjects),
            FilterBySelection(targetViews, selectedObjects),
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
        var (sourceFuncs, targetFuncs) = await FetchBothAsync(
            () => sourceProvider.GetFunctionsAsync(source, cancellationToken),
            () => targetProvider.GetFunctionsAsync(target, cancellationToken));

        return CompareObjectLists(
            FilterBySelection(sourceFuncs, selectedObjects),
            FilterBySelection(targetFuncs, selectedObjects),
            f => f.FullName,
            CompareType.Function,
            (s, t) => GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
    }

    private async Task<List<CompareResult>> CompareTriggersAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        HashSet<string> selectedObjects,
        CancellationToken cancellationToken)
    {
        var (sourceTriggers, targetTriggers) = await FetchBothAsync(
            () => sourceProvider.GetTriggersAsync(source, cancellationToken),
            () => targetProvider.GetTriggersAsync(target, cancellationToken));

        return CompareObjectLists(
            FilterBySelection(sourceTriggers, selectedObjects),
            FilterBySelection(targetTriggers, selectedObjects),
            t => t.FullName,
            CompareType.Trigger,
            (s, t) => GetTriggerDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareConstraintsAsync(
        IDatabaseProvider sourceProvider,
        IDatabaseProvider targetProvider,
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceConstraints, targetConstraints) = await FetchBothAsync(
            () => sourceProvider.GetConstraintsAsync(source, cancellationToken),
            () => targetProvider.GetConstraintsAsync(target, cancellationToken));

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
        CancellationToken cancellationToken)
    {
        var (sourceIndexes, targetIndexes) = await FetchBothAsync(
            () => sourceProvider.GetIndexesAsync(source, cancellationToken),
            () => targetProvider.GetIndexesAsync(target, cancellationToken));

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
        CancellationToken cancellationToken)
    {
        var tablesTask = provider.GetTablesAsync(connection, cancellationToken);
        var columnsTask = provider.GetColumnsAsync(connection, cancellationToken);
        var constraintsTask = provider.GetConstraintsAsync(connection, cancellationToken);
        var indexesTask = provider.GetIndexesAsync(connection, cancellationToken);

        await Task.WhenAll(tablesTask, columnsTask, constraintsTask, indexesTask);

        var tableMap = tablesTask.Result.ToDictionary(t => t.FullName, StringComparer.OrdinalIgnoreCase);

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

    private static List<T> FilterBySelection<T>(List<T> items, HashSet<string> selectedObjects)
        where T : SchemaObject
        => selectedObjects.Count == 0
            ? items
            : items.Where(i => selectedObjects.Contains(i.FullName)).ToList();

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

    private static List<CompareResult> CompareObjectLists<T>(
        List<T> sourceList,
        List<T> targetList,
        Func<T, string> keySelector,
        CompareType objectType,
        Func<T, T, List<string>> diffSelector) where T : SchemaObject
    {
        var results = new List<CompareResult>();
        var sourceMap = sourceList.ToDictionary(keySelector, x => x, StringComparer.OrdinalIgnoreCase);
        var targetMap = targetList.ToDictionary(keySelector, x => x, StringComparer.OrdinalIgnoreCase);
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
        => new()
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
        => new()
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
            diffs.Add($"Engine: {s.Engine} -> {t.Engine}");
        if (!string.Equals(s.Collation, t.Collation, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"Collation: {s.Collation} -> {t.Collation}");
        if (s.TotalRows != t.TotalRows)
            diffs.Add($"Approximate Rows: {s.TotalRows} -> {t.TotalRows}");
        return diffs;
    }

    private static List<string> GetColumnDifferences(ColumnSchema s, ColumnSchema t)
    {
        var diffs = new List<string>();
        if (s.DataTypeFull != t.DataTypeFull) diffs.Add($"DataType: {s.DataTypeFull} -> {t.DataTypeFull}");
        if (s.IsNullable != t.IsNullable) diffs.Add($"Nullable: {s.IsNullable} -> {t.IsNullable}");
        if (s.IsIdentity != t.IsIdentity) diffs.Add($"Identity: {s.IsIdentity} -> {t.IsIdentity}");
        if (s.IsPrimaryKey != t.IsPrimaryKey) diffs.Add($"PrimaryKey: {s.IsPrimaryKey} -> {t.IsPrimaryKey}");
        if (s.IsForeignKey != t.IsForeignKey) diffs.Add($"ForeignKey: {s.IsForeignKey} -> {t.IsForeignKey}");
        if (s.IsComputed != t.IsComputed) diffs.Add($"Computed: {s.IsComputed} -> {t.IsComputed}");
        if (!string.Equals(s.DefaultValue, t.DefaultValue, StringComparison.OrdinalIgnoreCase)) diffs.Add($"Default: {s.DefaultValue} -> {t.DefaultValue}");
        if (s.OrdinalPosition != t.OrdinalPosition) diffs.Add($"Position: {s.OrdinalPosition} -> {t.OrdinalPosition}");
        return diffs;
    }

    private static List<string> GetProcedureDifferences(ProcedureSchema s, ProcedureSchema t)
    {
        var diffs = GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition);
        if (!string.Equals(s.ReturnType, t.ReturnType, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"ReturnType: {s.ReturnType} -> {t.ReturnType}");
        if (!string.Equals(s.SchemaName, t.SchemaName, StringComparison.OrdinalIgnoreCase))
            diffs.Add($"Schema: {s.SchemaName} -> {t.SchemaName}");
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
