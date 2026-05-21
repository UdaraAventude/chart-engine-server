using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;

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
                    id = childKvp.Name,
                    label = childKvp.Name,
                    value = childKvp.Count
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
