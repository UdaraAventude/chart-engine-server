namespace ChartEngine.Application.DTOs;

public record DatasetSchemaDto(
    Guid DatasetId,
    List<string> Dimensions,
    List<string> Metrics
);
