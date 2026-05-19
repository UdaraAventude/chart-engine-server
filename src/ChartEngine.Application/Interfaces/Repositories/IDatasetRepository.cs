namespace ChartEngine.Application.Interfaces.Repositories;

using ChartEngine.Domain.Entities;

public interface IDatasetRepository
{
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Dataset dataset, CancellationToken ct = default);
    Task UpdateAsync(Dataset dataset, CancellationToken ct = default);
}
