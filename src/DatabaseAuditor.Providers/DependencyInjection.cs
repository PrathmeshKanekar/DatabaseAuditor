namespace DatabaseAuditor.Providers;

using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Providers.Factory;
using DatabaseAuditor.Providers.MariaDb;
using DatabaseAuditor.Providers.MySql;
using DatabaseAuditor.Providers.Oracle;
using DatabaseAuditor.Providers.PostgreSql;
using DatabaseAuditor.Providers.SqlServer;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddProviders(this IServiceCollection services)
    {
        // Register individual providers
        services.AddSingleton<SqlServerProvider>();
        services.AddSingleton<OracleProvider>();
        services.AddSingleton<PostgreSqlProvider>();
        services.AddSingleton<MySqlProvider>();
        services.AddSingleton<MariaDbProvider>();

        // Register factory
        services.AddSingleton<DatabaseProviderFactory>();

        // Register IDatabaseProvider via factory adapter
        services.AddSingleton<IDatabaseProvider>(provider =>
        {
            var factory = provider.GetRequiredService<DatabaseProviderFactory>();
            // Default to SqlServer; runtime resolution via factory per connection
            return factory.GetProvider(Domain.Enums.DatabaseType.SqlServer);
        });

        return services;
    }
}