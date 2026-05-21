namespace ChartEngine.Application.DTOs;

public record UploadCompletedResult(
    Guid DatasetId,
    int TotalRows,
    int Dimensions,
    int Metrics
);
