namespace DatabaseAuditor.Application.UseCases.CompareDatabase;

using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Domain.ValueObjects;

public class CompareDatabaseHandler
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly ICompareService _compareService;

    public CompareDatabaseHandler(
        IConnectionRepository connectionRepository,
        ICompareService compareService)
    {
        _connectionRepository = connectionRepository;
        _compareService = compareService;
    }

    public async Task<CompareSession> HandleAsync(
        CompareDatabaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = await _connectionRepository.GetByIdAsync(command.SourceConnectionId)
            ?? throw new InvalidOperationException($"Source connection '{command.SourceConnectionId}' not found.");

        var target = await _connectionRepository.GetByIdAsync(command.TargetConnectionId)
            ?? throw new InvalidOperationException($"Target connection '{command.TargetConnectionId}' not found.");

        var session = await _compareService.CompareAsync(
            source,
            target,
            command.CompareType,
            cancellationToken);

        if (!command.IncludeUnchanged)
        {
            session.Results.RemoveAll(r =>
                r.ChangeType == Domain.Enums.ChangeType.Unchanged);
        }

        return session;
    }
}