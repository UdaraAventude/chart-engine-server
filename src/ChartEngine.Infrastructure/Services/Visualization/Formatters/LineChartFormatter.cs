using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Domain.ValueObjects;

using ChartEngine.Infrastructure.Services.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization.Formatters;

public class LineChartFormatter : IChartFormatter
{
    public string ChartType => "line";

    public ChartVisualizationDto Format(TreeNode node, int level, string groupBy, string aggregation)
    {
        var series = ChartNodeDataHelper.BuildStandardSeries(node, aggregation);
        var points = series.Select(item =>
        {
            dynamic d = item;
            return new { x = (string)d.name, y = (double)d.value };
        }).Cast<object>().ToList();

        var data = new List<object>
        {
            new
            {
                id = groupBy == "default" ? "Total Count" : groupBy,
                data = points,
            },
        };

        return new ChartVisualizationDto
        {
            ChartType = ChartType,
            Data = data,
            Meta = VisualizationMetaBuilder.Build(node, level, groupBy),
        };
    }
}
