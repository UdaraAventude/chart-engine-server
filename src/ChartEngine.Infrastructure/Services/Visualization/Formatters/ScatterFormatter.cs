using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class ScatterFormatter : IChartFormatter
{
    public string ChartType => "scatter";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        var data = new List<object>();

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                data.Add(new
                {
                    name = child.Name,
                    x = child.Name,
                    y = ChartValueResolver.Resolve(child, aggregation),
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
