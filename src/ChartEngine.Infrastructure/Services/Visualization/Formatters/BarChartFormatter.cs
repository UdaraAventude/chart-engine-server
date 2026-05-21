using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class BarChartFormatter : IChartFormatter
{
    public string ChartType => "bar";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        var data = ChartNodeDataHelper.BuildStandardSeries(node, aggregation);

        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = data,
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
