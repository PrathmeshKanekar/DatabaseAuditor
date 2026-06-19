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

        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<IDatabaseProviderResolver>(provider =>
            provider.GetRequiredService<DatabaseProviderFactory>());

        return services;
    }
}
