using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class SunburstFormatter : IChartFormatter
{
    public string ChartType => "sunburst";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy)
    {
        object CreateHierarchy(TreeNode n, string name)
        {
            if (n.Children == null || !n.Children.Any())
                return new { name, loc = n.Count };

            return new { name, children = n.Children.Select(c => CreateHierarchy(c, c.Name)).ToList() };
        }

        var data = CreateHierarchy(node, "root");

        return new ChartVisualizationDto { ChartType = ChartType, Data = data, Meta = new VisualizationMetaDto { Level = level, NodesCount = node.Children?.Count ?? 0, GroupedBy = groupBy } };
    }
}
