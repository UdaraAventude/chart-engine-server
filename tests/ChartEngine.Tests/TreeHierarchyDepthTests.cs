using System.Text.Json;
using ChartEngine.Infrastructure.Analytics;
using Xunit;

namespace ChartEngine.Tests;

public class TreeHierarchyDepthTests
{
    [Fact]
    public void ComputeFromTreeElement_ReturnsDeepestChildPath()
    {
        const string json = """
            {
              "name": "root",
              "children": [
                {
                  "name": "A",
                  "children": [
                    { "name": "B", "children": [ { "name": "C" } ] }
                  ]
                }
              ]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var depth = TreeHierarchyDepth.ComputeFromTreeElement(doc.RootElement);

        Assert.Equal(3, depth);
    }
}
