namespace ChartEngine.Application.DTOs;

public class SampleRowsDto
{
    public IReadOnlyList<Dictionary<string, string>> Rows { get; set; } = Array.Empty<Dictionary<string, string>>();
    public IReadOnlyList<string> Dimensions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Metrics { get; set; } = Array.Empty<string>();
}
