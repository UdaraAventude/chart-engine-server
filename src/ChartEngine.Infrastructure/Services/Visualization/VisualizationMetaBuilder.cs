using ChartEngine.Application.DTOs;
using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization;

public static class VisualizationMetaBuilder
{
    public static VisualizationMetaDto Build(TreeNode node, int level, string groupBy)
    {
        var childCount = node.Children?.Count ?? 0;
        var hasLeafAggregate = childCount == 0 && (node.Count > 0 || node.Aggs.Count > 0);

        return new VisualizationMetaDto
        {
            Level = level,
            NodesCount = childCount > 0 ? childCount : (hasLeafAggregate ? 1 : 0),
            GroupedBy = groupBy,
            CanDrillDown = childCount > 0,
        };
    }
}
