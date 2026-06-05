namespace DatabaseAuditor.Domain.Interfaces;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.ValueObjects;

public interface ICompareService
{
    Task<CompareSession> CompareAsync(
        ConnectionProfile source,
        ConnectionProfile target,
        CompareType compareType,
        ComparisonScope comparisonScope,
        IReadOnlyCollection<string>? selectedObjects = null,
        CancellationToken cancellationToken = default);
}
