namespace ChartEngine.Application.Interfaces.Services;

using ChartEngine.Application.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

public interface IRowQueryService
{
    Task<PagedRowsDto?> GetPagedRowsAsync(
        Guid datasetId,
        int page,
        int pageSize,
        string? drillPathJson,
        CancellationToken ct = default);
}
