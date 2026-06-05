namespace DatabaseAuditor.Application.Services;

using DatabaseAuditor.Application.UseCases.ManageConnections;
using DatabaseAuditor.Application.Validators;
using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Interfaces;

public class ConnectionService
{
    private readonly IConnectionRepository _repository;
    private readonly IDatabaseProviderResolver _providerResolver;
    private readonly ConnectionProfileValidator _validator;

    public ConnectionService(
        IConnectionRepository repository,
        IDatabaseProviderResolver providerResolver,
        ConnectionProfileValidator validator)
    {
        _repository = repository;
        _providerResolver = providerResolver;
        _validator = validator;
    }

    public async Task<List<ConnectionProfile>> GetAllAsync()
        => await _repository.GetAllAsync();

    public async Task<ConnectionProfile?> GetByIdAsync(Guid id)
        => await _repository.GetByIdAsync(id);

    public async Task<ValidationResult> AddAsync(AddConnectionCommand command)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid) return validation;

        var profile = new ConnectionProfile
        {
            Name = command.Name,
            Environment = command.Environment,
            DatabaseType = command.DatabaseType,
            Server = command.Server,
            Port = command.Port,
            DatabaseName = command.DatabaseName,
            Username = command.Username,
            Password = command.Password
        };

        await _repository.AddAsync(profile);
        return validation;
    }

    public async Task<ValidationResult> UpdateAsync(EditConnectionCommand command)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid) return validation;

        var profile = await _repository.GetByIdAsync(command.Id)
            ?? throw new InvalidOperationException($"Connection '{command.Id}' not found.");

        profile.Name = command.Name;
        profile.Environment = command.Environment;
        profile.DatabaseType = command.DatabaseType;
        profile.Server = command.Server;
        profile.Port = command.Port;
        profile.DatabaseName = command.DatabaseName;
        profile.Username = command.Username;
        profile.Password = command.Password;
        profile.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(profile);
        return validation;
    }

    public async Task DeleteAsync(Guid id)
    {
        var exists = await _repository.ExistsAsync(id);
        if (!exists)
            throw new InvalidOperationException($"Connection '{id}' not found.");

        await _repository.DeleteAsync(id);
    }

    public async Task<bool> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var profile = await _repository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Connection '{id}' not found.");

        var provider = _providerResolver.GetProvider(profile.DatabaseType);
        return await provider.TestConnectionAsync(profile, cancellationToken);
    }

    public async Task<List<ConnectionProfile>> ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var profiles = System.Text.Json.JsonSerializer.Deserialize<List<ConnectionProfile>>(json)
            ?? [];

        foreach (var profile in profiles)
        {
            profile.Id = Guid.NewGuid();
            profile.CreatedAt = DateTime.UtcNow;
            profile.UpdatedAt = null;
            await _repository.AddAsync(profile);
        }

        return profiles;
    }

    public async Task ExportAsync(string filePath)
    {
        var profiles = await _repository.GetAllAsync();
        var json = System.Text.Json.JsonSerializer.Serialize(profiles,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }
}
