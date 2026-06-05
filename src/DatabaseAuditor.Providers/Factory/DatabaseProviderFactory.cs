namespace DatabaseAuditor.Providers.Factory;

using DatabaseAuditor.Domain.Enums;
using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Providers.MariaDb;
using DatabaseAuditor.Providers.MySql;
using DatabaseAuditor.Providers.Oracle;
using DatabaseAuditor.Providers.PostgreSql;
using DatabaseAuditor.Providers.SqlServer;

public class DatabaseProviderFactory : IDatabaseProviderResolver
{
    private readonly IReadOnlyDictionary<DatabaseType, IDatabaseProvider> _providers;

    public DatabaseProviderFactory()
    {
        _providers = new Dictionary<DatabaseType, IDatabaseProvider>
        {
            { DatabaseType.SqlServer,  new SqlServerProvider()  },
            { DatabaseType.Oracle,     new OracleProvider()     },
            { DatabaseType.PostgreSql, new PostgreSqlProvider() },
            { DatabaseType.MySql,      new MySqlProvider()      },
            { DatabaseType.MariaDb,    new MariaDbProvider()    }
        };
    }

    public IDatabaseProvider GetProvider(DatabaseType databaseType)
    {
        if (_providers.TryGetValue(databaseType, out var provider))
            return provider;

        throw new NotSupportedException(
            $"Database type '{databaseType}' is not supported.");
    }

    public IReadOnlyCollection<DatabaseType> GetSupportedTypes()
        => _providers.Keys.ToList().AsReadOnly();
}
