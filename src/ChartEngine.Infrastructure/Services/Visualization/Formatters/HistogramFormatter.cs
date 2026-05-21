using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class HistogramFormatter : IChartFormatter
{
    public string ChartType => "histogram";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        var data = new List<object>();
        if (node.Children != null)
        {
            foreach (var childKvp in node.Children)
            {
                data.Add(new { bin = childKvp.Key, frequency = childKvp.Value.Count });
            }
        }

        return new ChartVisualizationDto { ChartType = ChartType, Data = data, Meta = new VisualizationMetaDto { Level = level, NodesCount = node.Children?.Count ?? 0, GroupedBy = groupBy } };
    }
}
