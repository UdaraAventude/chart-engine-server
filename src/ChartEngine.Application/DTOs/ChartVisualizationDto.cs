namespace ChartEngine.Application.DTOs;

public class VisualizationMetaDto
{
    public int Level { get; set; }
    public int NodesCount { get; set; }
    public string GroupedBy { get; set; } = string.Empty;
}

public class ChartVisualizationDto
{
    public string ChartType { get; set; } = string.Empty;
    public object Data { get; set; } = new object();
    public VisualizationMetaDto Meta { get; set; } = new VisualizationMetaDto();
}
