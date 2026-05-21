using ChartEngine.Application.DTOs;

namespace ChartEngine.Application.Interfaces.Services;

public interface ISampleRowsService
{
    Task<SampleRowsDto> GetSampleRowsAsync(
        Guid datasetId,
        string? drillPathJson,
        int limit = 5000,
        CancellationToken ct = default);
}
