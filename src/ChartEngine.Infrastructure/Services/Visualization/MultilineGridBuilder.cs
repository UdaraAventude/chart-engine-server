using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization;

public static class MultilineGridBuilder
{
    public static object Build(TreeNode node, string aggregation, int limit = 50)
    {
        if (node.Children == null || node.Children.Count == 0)
        {
            return new { xAxisLabels = Array.Empty<string>(), series = Array.Empty<object>() };
        }

        var xSet = new HashSet<string>();
        foreach (var child in node.Children)
        {
            foreach (var gc in child.Children ?? [])
                xSet.Add(gc.Name);
        }

        var xAxisLabels = xSet.OrderBy(n => n).ToList();
        if (xAxisLabels.Count == 0)
        {
            return new { xAxisLabels = Array.Empty<string>(), series = Array.Empty<object>() };
        }

        var series = node.Children.Take(limit).Select(child =>
        {
            var gcMap = new Dictionary<string, double>();
            foreach (var gc in child.Children ?? [])
                gcMap[gc.Name] = ChartValueResolver.Resolve(gc, aggregation);

            var data = xAxisLabels.Select(x => gcMap.GetValueOrDefault(x, 0)).ToList();

            return new { name = child.Name, data };
        }).ToList();

        return new { xAxisLabels, series };
    }
}
