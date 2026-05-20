namespace ChartEngine.Application.Interfaces.Repositories;

using ChartEngine.Application.DTOs;
using ChartEngine.Domain.Entities;

public interface IDatasetRepository
{
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Dataset dataset, CancellationToken ct = default);
    Task UpdateAsync(Dataset dataset, CancellationToken ct = default);
    Task SaveColumnsAsync(IEnumerable<DatasetColumn> columns, CancellationToken ct = default);
    Task<IEnumerable<DatasetColumn>> GetColumnsAsync(Guid datasetId, CancellationToken ct = default);
    Task SaveConfigAsync(DatasetConfig config, CancellationToken ct = default);
}
