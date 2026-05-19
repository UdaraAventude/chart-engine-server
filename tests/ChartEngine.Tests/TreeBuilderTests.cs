namespace ChartEngine.Tests;

using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Analytics;
using Xunit;

public class TreeBuilderTests
{
    // Helper: write a temp CSV and return its path
    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Single_dimension_single_metric_builds_correct_tree()
    {
        var csvPath = WriteTempCsv("""
            Region,Sales
            North,100
            North,200
            South,300
            """);

        var schema = new DatasetSchema(
            Dimensions: ["Region"],
            Metrics: ["Sales"],
            Rejected: [],
            Config: DynamicConfigResolver.Resolve(3));

        var builder = new TreeBuilder();
        var (root, totalRows) = await builder.BuildAsync(csvPath, schema,
            onProgress: _ => Task.CompletedTask);

        // Root should aggregate ALL rows
        Assert.Equal(3, totalRows);
        Assert.Equal(3, root.Count);
        Assert.Equal(600, root.Aggs["Sales"].Sum);

        // Should have 2 children: North and South
        Assert.NotNull(root.Children);
        Assert.Equal(2, root.Children.Count);

        // North appears twice — should be first (sorted by count desc)
        var north = root.Children.First();
        Assert.Equal("North", north.Name);
        Assert.Equal(2, north.Count);
        Assert.Equal(300, north.Aggs["Sales"].Sum);
        Assert.Equal(150, north.Aggs["Sales"].Avg);

        var south = root.Children.Last();
        Assert.Equal("South", south.Name);
        Assert.Equal(1, south.Count);
        Assert.Equal(300, south.Aggs["Sales"].Sum);
    }

    [Fact]
    public async Task Two_dimensions_build_nested_tree()
    {
        var csvPath = WriteTempCsv("""
            Region,Category,Sales
            North,Electronics,500
            North,Clothing,200
            South,Electronics,300
            """);

        var schema = new DatasetSchema(
            Dimensions: ["Region", "Category"],
            Metrics: ["Sales"],
            Rejected: [],
            Config: DynamicConfigResolver.Resolve(3));

        var builder = new TreeBuilder();
        var (root, _) = await builder.BuildAsync(csvPath, schema,
            onProgress: _ => Task.CompletedTask);

        // Root → North → Electronics (500)
        //              → Clothing (200)
        //      → South → Electronics (300)

        var north = root.Children!.First(c => c.Name == "North");
        Assert.Equal(2, north.Children!.Count);
        Assert.Equal(700, north.Aggs["Sales"].Sum);

        var northElec = north.Children.First(c => c.Name == "Electronics");
        Assert.Equal(500, northElec.Aggs["Sales"].Sum);
        Assert.Null(northElec.Children);  // leaf node
    }

    [Fact]
    public async Task Min_max_avg_are_correct()
    {
        var csvPath = WriteTempCsv("""
            Region,Sales
            North,100
            North,300
            North,200
            """);

        var schema = new DatasetSchema(
            Dimensions: ["Region"],
            Metrics: ["Sales"],
            Rejected: [],
            Config: DynamicConfigResolver.Resolve(3));

        var builder = new TreeBuilder();
        var (root, _) = await builder.BuildAsync(csvPath, schema,
            onProgress: _ => Task.CompletedTask);

        var northAgg = root.Children!.First().Aggs["Sales"];
        Assert.Equal(100, northAgg.Min);
        Assert.Equal(300, northAgg.Max);
        Assert.Equal(200, northAgg.Avg);   // (100+300+200)/3
        Assert.Equal(600, northAgg.Sum);
        Assert.Equal(3, northAgg.Count);
    }

    [Fact]
    public async Task Children_sorted_by_count_descending()
    {
        var csvPath = WriteTempCsv("""
            Region,Sales
            North,100
            North,200
            North,300
            South,400
            """);

        var schema = new DatasetSchema(
            Dimensions: ["Region"],
            Metrics: ["Sales"],
            Rejected: [],
            Config: DynamicConfigResolver.Resolve(4));

        var builder = new TreeBuilder();
        var (root, _) = await builder.BuildAsync(csvPath, schema,
            onProgress: _ => Task.CompletedTask);

        // North has 3 rows, South has 1 — North must come first
        Assert.Equal("North", root.Children![0].Name);
        Assert.Equal("South", root.Children![1].Name);
    }
}
