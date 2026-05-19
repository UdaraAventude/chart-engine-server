namespace ChartEngine.Application.DTOs;

public record DatasetUploadResult(
    Guid DatasetId,
    string Status,
    string FileName
);
