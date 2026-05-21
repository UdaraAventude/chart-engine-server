using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class CorrelationFormatter : IChartFormatter
{
    public string ChartType => "correlation";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        // Correlation matrix requires metric pairs; not derivable from tree children alone.
        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = Array.Empty<object>(),
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
