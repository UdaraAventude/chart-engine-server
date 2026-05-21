using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class BubbleFormatter : IChartFormatter
{
    public string ChartType => "bubble";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        var data = new List<object>();
        if (node.Children != null)
        {
            var points = node.Children.Select(kvp => new { x = kvp.Name, y = kvp.Count, z = kvp.Count / 2 }).ToList();
            data.Add(new { id = groupBy, data = points });
        }

        return new ChartVisualizationDto { ChartType = ChartType, Data = data, Meta = new VisualizationMetaDto { Level = level, NodesCount = node.Children?.Count ?? 0, GroupedBy = groupBy } };
    }
}
