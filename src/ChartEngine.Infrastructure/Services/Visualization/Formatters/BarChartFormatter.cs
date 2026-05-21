using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.Entities;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class BarChartFormatter : IChartFormatter
{
    public string ChartType => "bar";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        // For a Bar chart, the data is typically an array of objects like { name: "Category", value: 100 }
        // We will project the children of the current node into this format.
        
        var data = new List<object>();

        if (node.Children != null)
        {
            foreach (var childKvp in node.Children)
            {
                var category = childKvp.Key;
                var childNode = childKvp.Value;
                
                data.Add(new
                {
                    name = category,
                    value = childNode.Count // Using count as a generic value metric
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
