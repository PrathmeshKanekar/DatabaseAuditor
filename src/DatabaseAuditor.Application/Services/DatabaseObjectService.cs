namespace DatabaseAuditor.Application.Services;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;

public class DatabaseObjectService
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IDatabaseProviderResolver _providerResolver;

    public DatabaseObjectService(
        IConnectionRepository connectionRepository,
        IDatabaseProviderResolver providerResolver)
    {
        _connectionRepository = connectionRepository;
        _providerResolver = providerResolver;
    }

    public async Task<List<string>> GetObjectNamesAsync(
        Guid sourceConnectionId,
        Guid targetConnectionId,
        CompareType compareType,
        CancellationToken cancellationToken = default)
    {
        var source = await GetConnectionAsync(sourceConnectionId);
        var target = await GetConnectionAsync(targetConnectionId);

        var sourceProvider = _providerResolver.GetProvider(source.DatabaseType);
        var targetProvider = _providerResolver.GetProvider(target.DatabaseType);

        var sourceTask = GetNamesAsync(sourceProvider, source, compareType, cancellationToken);
        var targetTask = GetNamesAsync(targetProvider, target, compareType, cancellationToken);

        await Task.WhenAll(sourceTask, targetTask);

        return sourceTask.Result
            .Union(targetTask.Result, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<ConnectionProfile> GetConnectionAsync(Guid connectionId)
        => await _connectionRepository.GetByIdAsync(connectionId)
            ?? throw new InvalidOperationException($"Connection '{connectionId}' not found.");

    private static Task<List<string>> GetNamesAsync(
        IDatabaseProvider provider,
        ConnectionProfile connection,
        CompareType compareType,
        CancellationToken cancellationToken)
        => compareType switch
        {
            CompareType.Table => provider.GetTableNamesAsync(connection, cancellationToken),
            CompareType.Procedure => provider.GetProcedureNamesAsync(connection, cancellationToken),
            CompareType.View => GetNamesFromObjectsAsync(provider.GetViewsAsync(connection, null, cancellationToken)),
            CompareType.Function => GetNamesFromObjectsAsync(provider.GetFunctionsAsync(connection, null, cancellationToken)),
            CompareType.Trigger => GetNamesFromObjectsAsync(provider.GetTriggersAsync(connection, null, cancellationToken)),
            _ => Task.FromResult(new List<string>())
        };

    private static async Task<List<string>> GetNamesFromObjectsAsync<T>(Task<List<T>> task)
        where T : SchemaObject
        => (await task)
            .Select(o => o.FullName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
