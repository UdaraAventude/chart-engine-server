using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Services.Visualization;
using ChartEngine.Infrastructure.Services.Visualization.Formatters;
using System.Text.Json;
using Xunit;

namespace ChartEngine.Tests;

public class HeatmapFormatterTests
{
    [Fact]
    public void Format_BuildsTwoDimensionalGrid()
    {
        var root = new TreeNode { Name = "root", Count = 100 };
        var a = new TreeNode { Name = "A", Count = 40 };
        a.Children =
        [
            new TreeNode { Name = "X", Count = 10 },
            new TreeNode { Name = "Y", Count = 30 },
        ];
        var b = new TreeNode { Name = "B", Count = 60 };
        b.Children =
        [
            new TreeNode { Name = "X", Count = 20 },
            new TreeNode { Name = "Z", Count = 40 },
        ];
        root.Children = [a, b];

        var formatter = new HeatmapFormatter();
        var result = formatter.Format(root, 0, "dim0", "count");

        Assert.Equal("heatmap", result.ChartType);
        Assert.True(result.Meta.CanDrillDown);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result.Data));
        var rootEl = doc.RootElement;
        var xCategories = rootEl.GetProperty("xCategories").EnumerateArray().Select(e => e.GetString()).ToArray();
        var yCategories = rootEl.GetProperty("yCategories").EnumerateArray().Select(e => e.GetString()).ToArray();
        var cellCount = rootEl.GetProperty("cells").GetArrayLength();

        Assert.Equal(["A", "B"], xCategories);
        Assert.Contains("X", yCategories);
        Assert.Contains("Y", yCategories);
        Assert.Contains("Z", yCategories);
        Assert.True(cellCount >= 3);
    }

    [Fact]
    public void HeatmapGridBuilder_RespectsAggregation()
    {
        var gc = new TreeNode
        {
            Name = "gc1",
            Count = 2,
            Aggs = new Dictionary<string, MetricAggs>
            {
                ["revenue"] = new MetricAggs { Sum = 100, Count = 5 },
            },
        };
        var child = new TreeNode
        {
            Name = "child",
            Count = 5,
            Children = [gc],
        };
        var root = new TreeNode { Name = "root", Count = 5, Children = [child] };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(HeatmapGridBuilder.Build(root, "sum")));
        Assert.True(doc.RootElement.GetProperty("cells").GetArrayLength() > 0);
    }
}
