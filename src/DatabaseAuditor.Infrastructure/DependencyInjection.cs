namespace DatabaseAuditor.Infrastructure;

using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Infrastructure.Logging;
using DatabaseAuditor.Infrastructure.Persistence;
using DatabaseAuditor.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Encryption
        services.AddSingleton<IEncryptionService, EncryptionService>();

        // Repositories
        services.AddSingleton<IConnectionRepository, ConnectionRepository>();
        services.AddSingleton<SettingsRepository>();

        // Settings - load synchronously at startup
        services.AddSingleton<AppSettings>(provider =>
        {
            var repo = provider.GetRequiredService<SettingsRepository>();
            return repo.LoadAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    public static IServiceCollection AddLogging(
        this IServiceCollection services,
        AppSettings settings)
    {
        LoggerService.Initialize(settings);
        services.AddLogging(builder => builder.AddSerilog(dispose: true));
        return services;
    }
}