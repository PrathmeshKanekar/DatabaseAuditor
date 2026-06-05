namespace DatabaseAuditor.Domain.Interfaces;

using DatabaseAuditor.Domain.Enums;

public interface IDatabaseProviderResolver
{
    IDatabaseProvider GetProvider(DatabaseType databaseType);
}
