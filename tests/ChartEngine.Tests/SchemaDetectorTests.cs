namespace ChartEngine.Tests;

using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Analytics;
using Xunit;

public class SchemaDetectorTests
{
    private readonly SchemaDetector _detector = new();

    // Helper: build sample rows from a column dictionary
    private static List<Dictionary<string, string>> MakeRows(
        Dictionary<string, string[]> columns)
    {
        int rowCount = columns.Values.First().Length;
        return Enumerable.Range(0, rowCount)
            .Select(i => columns.ToDictionary(
                kv => kv.Key,
                kv => kv.Value[i]))
            .ToList();
    }

    [Fact]
    public void Numeric_column_is_classified_as_metric()
    {
        var rows = MakeRows(new() {
            ["Region"] = ["North", "South", "East"],
            ["Sales"]  = ["100.5", "200.0", "350.75"]
        });

        var schema = _detector.Detect(rows, estimatedRowCount: 3);

        Assert.Contains("Sales", schema.Metrics);
        Assert.Contains("Region", schema.Dimensions);
    }

    [Fact]
    public void High_cardinality_column_is_rejected()
    {
        // 600 unique values > default maxDimCardinality of 500 for small datasets
        var uniqueValues = Enumerable.Range(1, 600)
            .Select(i => $"P{i}")
            .ToArray();

        var rows = MakeRows(new() {
            ["ProductId"] = uniqueValues,
            ["Sales"]     = uniqueValues.Select(_ => "100").ToArray(),
            ["Category"]  = uniqueValues.Select(_ => "Electronics").ToArray() // provide a valid dimension
        });

        var schema = _detector.Detect(rows, estimatedRowCount: 600);

        var rejected = schema.Rejected.FirstOrDefault(r => r.Name == "ProductId");
        Assert.NotNull(rejected);
        Assert.Equal("too_many_unique", rejected.RejectionReason);
    }

    [Fact]
    public void Date_column_is_rejected()
    {
        var rows = MakeRows(new() {
            ["OrderDate"] = ["2024-01-15", "2024-02-20", "2024-03-10"],
            ["Region"]    = ["North", "South", "East"],
            ["Sales"]     = ["100", "200", "300"]
        });

        var schema = _detector.Detect(rows, estimatedRowCount: 3);

        var rejected = schema.Rejected.FirstOrDefault(r => r.Name == "OrderDate");
        Assert.NotNull(rejected);
        Assert.Equal("date_column", rejected.RejectionReason);
    }

    [Fact]
    public void Large_dataset_gets_lower_cardinality_limit()
    {
        // For > 1M rows, maxDimCardinality = 50
        var schema = _detector.Detect([], estimatedRowCount: 2_000_000);
        Assert.Equal(50, schema.Config.MaxDimCardinality);
    }

    [Fact]
    public void Small_dataset_gets_higher_cardinality_limit()
    {
        var schema = _detector.Detect([], estimatedRowCount: 5_000);
        Assert.Equal(500, schema.Config.MaxDimCardinality);
    }

    [Fact]
    public void Empty_rows_returns_empty_schema()
    {
        var schema = _detector.Detect([], estimatedRowCount: 0);
        Assert.Empty(schema.Dimensions);
        Assert.Empty(schema.Metrics);
    }
}
