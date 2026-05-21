using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization;

public static class ChartValueResolver
{
    public static double Resolve(TreeNode node, string aggregation)
    {
        if (node.Aggs != null && node.Aggs.Count > 0)
        {
            var agg = node.Aggs.Values.First();
            return aggregation.ToLowerInvariant() switch
            {
                "sum" => agg.Sum,
                "min" => agg.Min == double.MaxValue ? 0 : agg.Min,
                "max" => agg.Max == double.MinValue ? 0 : agg.Max,
                "avg" or "average" => agg.Avg,
                _ => node.Count,
            };
        }

        return node.Count;
    }
}
