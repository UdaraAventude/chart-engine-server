using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class PieChartFormatter : IChartFormatter
{
    public string ChartType => "pie";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        var series = ChartNodeDataHelper.BuildStandardSeries(node, aggregation);
        var data = series.Select(item =>
        {
            dynamic d = item;
            return new
            {
                id = (string)d.name,
                label = (string)d.name,
                name = (string)d.name,
                value = (double)d.value,
            };
        }).Cast<object>().ToList();

        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = data,
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
