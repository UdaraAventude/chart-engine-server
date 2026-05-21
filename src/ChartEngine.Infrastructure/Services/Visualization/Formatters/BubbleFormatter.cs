using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class BubbleFormatter : IChartFormatter
{
    public string ChartType => "bubble";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        var data = new List<object>();

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                var value = ChartValueResolver.Resolve(child, aggregation);
                data.Add(new
                {
                    name = child.Name,
                    x = value,
                    y = value,
                    size = value,
                    count = child.Count,
                });
            }
        }

        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = data,
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
