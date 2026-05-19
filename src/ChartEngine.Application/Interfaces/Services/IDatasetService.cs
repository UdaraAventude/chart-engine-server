namespace ChartEngine.Application.Interfaces.Services;

using ChartEngine.Application.DTOs;
using Microsoft.AspNetCore.Http;

public interface IDatasetService
{
    Task<DatasetUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default);
    Task<DatasetStatusDto> GetStatusAsync(Guid datasetId, CancellationToken ct = default);
}
