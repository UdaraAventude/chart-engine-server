using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class SunburstFormatter : IChartFormatter
{
    public string ChartType => "sunburst";

    private const int MaxDisplayDepth = 4;

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        object CreateHierarchy(TreeNode n, string name, int depth)
        {
            if (n.Children == null || n.Children.Count == 0 || depth >= MaxDisplayDepth)
            {
                return new
                {
                    name,
                    value = ChartValueResolver.Resolve(n, aggregation),
                    count = n.Count,
                };
            }

            return new
            {
                name,
                children = n.Children
                    .Select(c => CreateHierarchy(c, c.Name, depth + 1))
                    .ToList(),
            };
        }

        var data = CreateHierarchy(node, "root", 0);

        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = data,
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
