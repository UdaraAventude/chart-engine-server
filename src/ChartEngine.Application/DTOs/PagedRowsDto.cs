namespace ChartEngine.Application.DTOs;

using System.Collections.Generic;

public class PagedRowsDto
{
    public List<Dictionary<string, string>> Rows { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
