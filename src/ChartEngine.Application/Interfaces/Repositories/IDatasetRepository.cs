namespace ChartEngine.Application.Interfaces.Repositories;

using ChartEngine.Application.DTOs;
using ChartEngine.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IDatasetRepository
{
    Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Dataset dataset, CancellationToken ct = default);
    Task UpdateAsync(Dataset dataset, CancellationToken ct = default);
    Task SaveColumnsAsync(IEnumerable<DatasetColumn> columns, CancellationToken ct = default);
    Task<IEnumerable<DatasetColumn>> GetColumnsAsync(Guid datasetId, CancellationToken ct = default);
    Task SaveConfigAsync(DatasetConfig config, CancellationToken ct = default);
    Task<DatasetConfig?> GetConfigAsync(Guid datasetId, CancellationToken ct = default);
    Task<PagedListDto<DatasetListDto>> GetPagedDatasetsAsync(
        int page,
        int pageSize,
        string? sortBy,
        string? search,
        CancellationToken ct = default);
    Task DeleteAsync(Dataset dataset, CancellationToken ct = default);
}
