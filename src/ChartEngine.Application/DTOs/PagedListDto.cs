namespace ChartEngine.Application.DTOs;

using System.Collections.Generic;

public record PagedListDto<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
