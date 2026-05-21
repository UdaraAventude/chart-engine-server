using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class CorrelationFormatter : IChartFormatter
{
    public string ChartType => "correlation";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        var data = new List<object>();
        if (node.Children != null)
        {
            var points = node.Children.Select(c => new { x = c.Name, y = c.Count, r = (double)c.Count / 10 }).ToList();
            data.Add(new { id = groupBy, data = points });
        }

        return new ChartVisualizationDto { ChartType = ChartType, Data = data, Meta = new VisualizationMetaDto { Level = level, NodesCount = node.Children?.Count ?? 0, GroupedBy = groupBy } };
    }
}
