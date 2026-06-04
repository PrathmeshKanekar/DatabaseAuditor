namespace DatabaseAuditor.Application.Services;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Domain.ValueObjects;

public class CompareService : ICompareService
{
    private readonly IDatabaseProvider _provider;

    public CompareService(IDatabaseProvider provider)
    {
        _provider = provider;
    }

    public async Task<CompareSession> CompareAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CompareType compareType,
        CancellationToken cancellationToken = default)
    {
        var session = new CompareSession
        {
            Source = source,
            Target = target,
            CompareType = compareType,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            session.Results = compareType switch
            {
                CompareType.Table => await CompareTablesAsync(source, target, cancellationToken),
                CompareType.Column => await CompareColumnsAsync(source, target, cancellationToken),
                CompareType.Procedure => await CompareProceduresAsync(source, target, cancellationToken),
                CompareType.View => await CompareViewsAsync(source, target, cancellationToken),
                CompareType.Function => await CompareFunctionsAsync(source, target, cancellationToken),
                CompareType.Trigger => await CompareTriggersAsync(source, target, cancellationToken),
                CompareType.Constraint => await CompareConstraintsAsync(source, target, cancellationToken),
                CompareType.Index => await CompareIndexesAsync(source, target, cancellationToken),
                CompareType.Database => await CompareDatabaseAsync(source, target, cancellationToken),
                _ => throw new NotSupportedException($"CompareType '{compareType}' is not supported.")
            };

            session.IsSuccess = true;
        }
        catch (Exception ex)
        {
            session.IsSuccess = false;
            session.ErrorMessage = ex.Message;
        }
        finally
        {
            session.CompletedAt = DateTime.UtcNow;
        }

        return session;
    }

    private async Task<List<CompareResult>> CompareTablesAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceTables, targetTables) = await FetchBothAsync(
            () => _provider.GetTablesAsync(source, cancellationToken),
            () => _provider.GetTablesAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceTables,
            targetTables,
            t => t.FullName,
            CompareType.Table,
            (s, t) => GetTableDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareColumnsAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceColumns, targetColumns) = await FetchBothAsync(
            () => _provider.GetColumnsAsync(source, cancellationToken),
            () => _provider.GetColumnsAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceColumns,
            targetColumns,
            c => $"{c.TableName}.{c.FullName}",
            CompareType.Column,
            (s, t) => GetColumnDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareProceduresAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceProcs, targetProcs) = await FetchBothAsync(
            () => _provider.GetProceduresAsync(source, cancellationToken),
            () => _provider.GetProceduresAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceProcs,
            targetProcs,
            p => p.FullName,
            CompareType.Procedure,
            (s, t) => GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
    }

    private async Task<List<CompareResult>> CompareViewsAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceViews, targetViews) = await FetchBothAsync(
            () => _provider.GetViewsAsync(source, cancellationToken),
            () => _provider.GetViewsAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceViews,
            targetViews,
            v => v.FullName,
            CompareType.View,
            (s, t) => GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
    }

    private async Task<List<CompareResult>> CompareFunctionsAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceFuncs, targetFuncs) = await FetchBothAsync(
            () => _provider.GetFunctionsAsync(source, cancellationToken),
            () => _provider.GetFunctionsAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceFuncs,
            targetFuncs,
            f => f.FullName,
            CompareType.Function,
            (s, t) => GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
    }

    private async Task<List<CompareResult>> CompareTriggersAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceTriggers, targetTriggers) = await FetchBothAsync(
            () => _provider.GetTriggersAsync(source, cancellationToken),
            () => _provider.GetTriggersAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceTriggers,
            targetTriggers,
            t => t.FullName,
            CompareType.Trigger,
            (s, t) => GetTriggerDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareConstraintsAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceConstraints, targetConstraints) = await FetchBothAsync(
            () => _provider.GetConstraintsAsync(source, cancellationToken),
            () => _provider.GetConstraintsAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceConstraints,
            targetConstraints,
            c => $"{c.TableName}.{c.FullName}",
            CompareType.Constraint,
            (s, t) => GetConstraintDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareIndexesAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var (sourceIndexes, targetIndexes) = await FetchBothAsync(
            () => _provider.GetIndexesAsync(source, cancellationToken),
            () => _provider.GetIndexesAsync(target, cancellationToken));

        return CompareObjectLists(
            sourceIndexes,
            targetIndexes,
            i => $"{i.TableName}.{i.FullName}",
            CompareType.Index,
            (s, t) => GetIndexDifferences(s, t));
    }

    private async Task<List<CompareResult>> CompareDatabaseAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CancellationToken cancellationToken)
    {
        var results = new List<CompareResult>();

        var allTasks = new[]
        {
            CompareTablesAsync(source, target, cancellationToken),
            CompareColumnsAsync(source, target, cancellationToken),
            CompareProceduresAsync(source, target, cancellationToken),
            CompareViewsAsync(source, target, cancellationToken),
            CompareFunctionsAsync(source, target, cancellationToken),
            CompareTriggersAsync(source, target, cancellationToken),
            CompareConstraintsAsync(source, target, cancellationToken),
            CompareIndexesAsync(source, target, cancellationToken)
        };

        var allResults = await Task.WhenAll(allTasks);

        foreach (var r in allResults)
            results.AddRange(r);

        return results;
    }

    private static async Task<(List<T> Source, List<T> Target)> FetchBothAsync<T>(
        Func<Task<List<T>>> sourceFunc,
        Func<Task<List<T>>> targetFunc)
    {
        var sourceTask = sourceFunc();
        var targetTask = targetFunc();
        await Task.WhenAll(sourceTask, targetTask);
        return (sourceTask.Result, targetTask.Result);
    }

    private static List<CompareResult> CompareObjectLists<T>(
        List<T> sourceList,
        List<T> targetList,
        Func<T, string> keySelector,
        CompareType objectType,
        Func<T, T, List<string>> diffSelector) where T : SchemaObject
    {
        var results = new List<CompareResult>();
        var sourceMap = sourceList.ToDictionary(keySelector, x => x);
        var targetMap = targetList.ToDictionary(keySelector, x => x);
        var allKeys = sourceMap.Keys.Union(targetMap.Keys).Distinct();

        foreach (var key in allKeys)
        {
            var inSource = sourceMap.TryGetValue(key, out var sourceObj);
            var inTarget = targetMap.TryGetValue(key, out var targetObj);

            var result = new CompareResult
            {
                ObjectType = objectType,
                ObjectName = inSource ? sourceObj!.Name : targetObj!.Name,
                SchemaName = inSource ? sourceObj!.Schema : targetObj!.Schema
            };

            if (inSource && !inTarget)
            {
                result.ChangeType = ChangeType.Deleted;
                result.Status = ObjectStatus.MissingInTarget;
                result.SourceValue = key;
            }
            else if (!inSource && inTarget)
            {
                result.ChangeType = ChangeType.Added;
                result.Status = ObjectStatus.MissingInSource;
                result.TargetValue = key;
            }
            else
            {
                var diffs = diffSelector(sourceObj!, targetObj!);
                if (diffs.Count > 0)
                {
                    result.ChangeType = ChangeType.Modified;
                    result.Status = ObjectStatus.Mismatch;
                    result.Differences = diffs;
                }
                else
                {
                    result.ChangeType = ChangeType.Unchanged;
                    result.Status = ObjectStatus.Match;
                }

                result.SourceValue = key;
                result.TargetValue = key;
            }

            results.Add(result);
        }

        return results;
    }

    private static List<string> GetTableDifferences(TableSchema s, TableSchema t)
    {
        var diffs = new List<string>();
        if (s.Engine != t.Engine) diffs.Add($"Engine: {s.Engine} → {t.Engine}");
        if (s.Collation != t.Collation) diffs.Add($"Collation: {s.Collation} → {t.Collation}");
        return diffs;
    }

    private static List<string> GetColumnDifferences(ColumnSchema s, ColumnSchema t)
    {
        var diffs = new List<string>();
        if (s.DataTypeFull != t.DataTypeFull) diffs.Add($"DataType: {s.DataTypeFull} → {t.DataTypeFull}");
        if (s.IsNullable != t.IsNullable) diffs.Add($"Nullable: {s.IsNullable} → {t.IsNullable}");
        if (s.IsIdentity != t.IsIdentity) diffs.Add($"Identity: {s.IsIdentity} → {t.IsIdentity}");
        if (s.DefaultValue != t.DefaultValue) diffs.Add($"Default: {s.DefaultValue} → {t.DefaultValue}");
        if (s.OrdinalPosition != t.OrdinalPosition) diffs.Add($"Position: {s.OrdinalPosition} → {t.OrdinalPosition}");
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
        if (s.TriggerEvent != t.TriggerEvent) diffs.Add($"Event: {s.TriggerEvent} → {t.TriggerEvent}");
        if (s.ActionTiming != t.ActionTiming) diffs.Add($"Timing: {s.ActionTiming} → {t.ActionTiming}");
        if (s.IsEnabled != t.IsEnabled) diffs.Add($"Enabled: {s.IsEnabled} → {t.IsEnabled}");
        diffs.AddRange(GetDefinitionDifferences(s.NormalizedDefinition, t.NormalizedDefinition));
        return diffs;
    }

    private static List<string> GetConstraintDifferences(ConstraintSchema s, ConstraintSchema t)
    {
        var diffs = new List<string>();
        if (s.ConstraintType != t.ConstraintType) diffs.Add($"Type: {s.ConstraintType} → {t.ConstraintType}");
        if (s.ColumnName != t.ColumnName) diffs.Add($"Column: {s.ColumnName} → {t.ColumnName}");
        if (s.CheckClause != t.CheckClause) diffs.Add($"CheckClause: {s.CheckClause} → {t.CheckClause}");
        return diffs;
    }

    private static List<string> GetIndexDifferences(IndexSchema s, IndexSchema t)
    {
        var diffs = new List<string>();
        if (s.IndexType != t.IndexType) diffs.Add($"Type: {s.IndexType} → {t.IndexType}");
        if (s.IsUnique != t.IsUnique) diffs.Add($"Unique: {s.IsUnique} → {t.IsUnique}");
        if (s.IsClustered != t.IsClustered) diffs.Add($"Clustered: {s.IsClustered} → {t.IsClustered}");
        var sourceCols = string.Join(",", s.Columns.OrderBy(c => c));
        var targetCols = string.Join(",", t.Columns.OrderBy(c => c));
        if (sourceCols != targetCols) diffs.Add($"Columns: [{sourceCols}] → [{targetCols}]");
        return diffs;
    }
}