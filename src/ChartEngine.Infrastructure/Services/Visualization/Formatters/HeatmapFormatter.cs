using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class HeatmapFormatter : IChartFormatter
{
    public string ChartType => "heatmap";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = HeatmapGridBuilder.Build(node, aggregation),
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
