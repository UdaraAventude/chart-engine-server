using System;

namespace ChartEngine.Application.DTOs;

public record ExportJobStatusDto(
    Guid JobId,
    Guid DatasetId,
    string Format,
    string Status,
    DateTime CreatedAt,
    string? DownloadUrl,
    string? ErrorMessage
);
