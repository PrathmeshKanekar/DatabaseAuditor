namespace DatabaseAuditor.Application.UseCases.SyncColumns;

using System;
using System.Threading;
using System.Threading.Tasks;
using DatabaseAuditor.Domain.Interfaces;

public class SyncColumnsHandler
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly IDatabaseProviderResolver _providerResolver;

    public SyncColumnsHandler(
        IConnectionRepository connectionRepository,
        IDatabaseProviderResolver providerResolver)
    {
        _connectionRepository = connectionRepository;
        _providerResolver = providerResolver;
    }

    public async Task HandleAsync(SyncColumnsCommand command, CancellationToken cancellationToken = default)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (command.SyncScripts == null || command.SyncScripts.Count == 0) return;

        var target = await _connectionRepository.GetByIdAsync(command.TargetConnectionId)
            ?? throw new InvalidOperationException($"Target connection '{command.TargetConnectionId}' not found.");

        var provider = _providerResolver.GetProvider(target.DatabaseType);

        foreach (var sql in command.SyncScripts)
        {
            if (string.IsNullOrWhiteSpace(sql)) continue;
            await provider.ExecuteSqlAsync(target, sql, cancellationToken);
        }
    }
}
