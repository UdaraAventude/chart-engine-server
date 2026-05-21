namespace ChartEngine.Application.DTOs;

public record UploadCompletedResult(
    Guid DatasetId,
    int TotalRows,
    int Dimensions,
    int Metrics,
    int MaxHierarchyDepth,
    IReadOnlyList<string> DimensionColumns,
    IReadOnlyList<string> MetricColumns
);
