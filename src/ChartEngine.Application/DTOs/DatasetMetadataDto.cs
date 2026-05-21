namespace ChartEngine.Application.DTOs;

public record RejectedColumnDto(string Key, string Reason, int Cardinality);

public record DatasetMetadataDto(
    Guid DatasetId,
    string FileName,
    string Status,
    int TotalRows,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<RejectedColumnDto> Rejected,
    int MaxHierarchyDepth
);

public record TreeEnvelopeMetadata(
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<RejectedColumnDto> Rejected,
    int TotalRows,
    int MaxHierarchyDepth
);
