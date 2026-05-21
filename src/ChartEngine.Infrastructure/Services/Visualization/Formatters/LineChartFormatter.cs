using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class LineChartFormatter : IChartFormatter
{
    public string ChartType => "line";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        // Line chart expects an array of series: [{ id: "Series 1", data: [{ x: "Jan", y: 10 }] }]
        
        var points = new List<object>();

        if (node.Children != null)
        {
            foreach (var childKvp in node.Children)
            {
                points.Add(new
                {
                    x = childKvp.Key,
                    y = childKvp.Value.Count
                });
            }
        }

        var data = new List<object>
        {
            new
            {
                id = groupBy == "default" ? "Total Count" : groupBy,
                data = points
            }
        };

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
