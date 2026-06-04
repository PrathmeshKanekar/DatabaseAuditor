namespace DatabaseAuditor.Infrastructure.Persistence;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.Domain.Interfaces;
using System.Text.Json;

public class ConnectionRepository : IConnectionRepository
{
    private readonly IEncryptionService _encryption;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConnectionRepository(IEncryptionService encryption)
    {
        _encryption = encryption;
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ThreeStarInfotech",
            "DatabaseAuditor",
            "connections.dat");

        EnsureDirectoryExists();
    }

    public async Task<List<ConnectionProfile>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
                return [];

            var encrypted = await File.ReadAllTextAsync(_filePath);
            if (string.IsNullOrWhiteSpace(encrypted))
                return [];

            var json = _encryption.Decrypt(encrypted);
            return JsonSerializer.Deserialize<List<ConnectionProfile>>(json, _jsonOptions) ?? [];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ConnectionProfile?> GetByIdAsync(Guid id)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(c => c.Id == id);
    }

    public async Task AddAsync(ConnectionProfile profile)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadUnsafeAsync();
            all.Add(profile);
            await SaveUnsafeAsync(all);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(ConnectionProfile profile)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadUnsafeAsync();
            var index = all.FindIndex(c => c.Id == profile.Id);
            if (index < 0)
                throw new InvalidOperationException($"Connection '{profile.Id}' not found.");

            all[index] = profile;
            await SaveUnsafeAsync(all);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            var all = await LoadUnsafeAsync();
            var removed = all.RemoveAll(c => c.Id == id);
            if (removed == 0)
                throw new InvalidOperationException($"Connection '{id}' not found.");

            await SaveUnsafeAsync(all);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        var all = await GetAllAsync();
        return all.Any(c => c.Id == id);
    }

    // Unsafe = called only when lock is already held
    private async Task<List<ConnectionProfile>> LoadUnsafeAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        var encrypted = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(encrypted))
            return [];

        var json = _encryption.Decrypt(encrypted);
        return JsonSerializer.Deserialize<List<ConnectionProfile>>(json, _jsonOptions) ?? [];
    }

    private async Task SaveUnsafeAsync(List<ConnectionProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, _jsonOptions);
        var encrypted = _encryption.Encrypt(json);
        await File.WriteAllTextAsync(_filePath, encrypted);
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }
}