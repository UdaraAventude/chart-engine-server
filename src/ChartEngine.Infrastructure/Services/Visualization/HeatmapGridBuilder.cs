using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Infrastructure.Services.Visualization;

public static class HeatmapGridBuilder
{
    public static object Build(TreeNode node, string aggregation, int limit = 50)
    {
        if (node.Children == null || node.Children.Count == 0)
        {
            return new { xCategories = Array.Empty<string>(), yCategories = Array.Empty<string>(), cells = Array.Empty<object>() };
        }

        var children = node.Children.Take(limit).ToList();
        var xCategories = children.Select(c => c.Name).ToList();

        var ySet = new HashSet<string>();
        foreach (var child in children)
        {
            if (child.Children == null) continue;
            foreach (var gc in child.Children)
                ySet.Add(gc.Name);
        }

        var yCategories = ySet.OrderBy(n => n).ToList();
        var cells = new List<object>();

        for (var xIndex = 0; xIndex < children.Count; xIndex++)
        {
            var child = children[xIndex];
            if (child.Children == null) continue;

            foreach (var gc in child.Children)
            {
                var yIndex = yCategories.IndexOf(gc.Name);
                if (yIndex < 0) continue;

                cells.Add(new
                {
                    x = xIndex,
                    y = yIndex,
                    value = ChartValueResolver.Resolve(gc, aggregation),
                    xLabel = child.Name,
                    yLabel = gc.Name,
                    count = gc.Count,
                });
            }
        }

        return new { xCategories, yCategories, cells };
    }
}
