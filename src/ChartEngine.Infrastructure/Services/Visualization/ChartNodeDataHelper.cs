using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization;

public static class ChartNodeDataHelper
{
    /// <summary>
    /// Children for charts, or a single aggregate bar when the stored tree ends before schema dimensions.
    /// </summary>
    public static List<object> BuildStandardSeries(TreeNode node, string aggregation)
    {
        if (node.Children is { Count: > 0 })
        {
            return node.Children.Select(child => new
            {
                name = child.Name,
                value = ChartValueResolver.Resolve(child, aggregation),
                count = child.Count,
            }).Cast<object>().ToList();
        }

        if (node.Count <= 0 && node.Aggs.Count == 0)
            return new List<object>();

        return new List<object>
        {
            new
            {
                name = string.IsNullOrEmpty(node.Name) || node.Name == "root" ? "Total" : node.Name,
                value = ChartValueResolver.Resolve(node, aggregation),
                count = node.Count,
            },
        };
    }
}
