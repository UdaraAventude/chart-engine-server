using ChartEngine.Application.DTOs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.Application.Interfaces.Services;

public interface IExportService
{
    Task<ExportJobStatusDto> StartExportAsync(Guid datasetId, string format, string? drillPathJson, CancellationToken ct = default);
    Task<ExportJobStatusDto?> GetStatusAsync(Guid jobId, CancellationToken ct = default);
    Task<(Stream FileStream, string FileName, string ContentType)> DownloadExportAsync(Guid jobId, CancellationToken ct = default);
    Task ProcessExportAsync(Guid jobId, CancellationToken ct = default);
}
