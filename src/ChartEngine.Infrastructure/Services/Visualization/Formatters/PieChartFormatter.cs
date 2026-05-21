using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class PieChartFormatter : IChartFormatter
{
    public string ChartType => "pie";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        var data = new List<object>();

        if (node.Children != null)
        {
            foreach (var childKvp in node.Children)
            {
                data.Add(new
                {
                    id = childKvp.Key,
                    label = childKvp.Key,
                    value = childKvp.Value.Count
                });
            }
        }

        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = data,
            Meta = new VisualizationMetaDto
            {
                Level = level,
                NodesCount = node.Children?.Count ?? 0,
                GroupedBy = groupBy
            }
        };
    }
}
