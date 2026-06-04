namespace DatabaseAuditor.Infrastructure;

using DatabaseAuditor.Domain.Interfaces;
using DatabaseAuditor.Infrastructure.Logging;
using DatabaseAuditor.Infrastructure.Persistence;
using DatabaseAuditor.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

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
            return repo.Load();
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