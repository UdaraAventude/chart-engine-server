namespace ChartEngine.Application.DTOs;

public record DatasetStatusDto(
    Guid DatasetId,
    string FileName,
    string Status,
    int TotalRows,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? ErrorMessage
);
