namespace ChartEngine.Application.DTOs;

using System;

public record DatasetListDto(
    Guid Id,
    string FileName,
    string Status,
    int TotalRows,
    DateTime CreatedAt
);
