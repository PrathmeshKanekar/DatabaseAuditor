namespace DatabaseAuditor.Domain.Interfaces;

using DatabaseAuditor.Domain.Entities;

public interface IConnectionRepository
{
    Task<List<ConnectionProfile>> GetAllAsync();
    Task<ConnectionProfile?> GetByIdAsync(Guid id);
    Task AddAsync(ConnectionProfile profile);
    Task UpdateAsync(ConnectionProfile profile);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}