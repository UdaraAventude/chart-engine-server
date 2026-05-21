using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class MultilineFormatter : IChartFormatter
{
    public string ChartType => "multiline";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        var data = new List<object>();
        if (node.Children != null)
        {
            // Just simulate multiple series
            data.Add(new { id = "Series A", data = node.Children.Select(c => new { x = c.Name, y = c.Count }).ToList() });
            data.Add(new { id = "Series B", data = node.Children.Select(c => new { x = c.Name, y = c.Count / 2 }).ToList() });
        }

        return new ChartVisualizationDto { ChartType = ChartType, Data = data, Meta = new VisualizationMetaDto { Level = level, NodesCount = node.Children?.Count ?? 0, GroupedBy = groupBy } };
    }
}
