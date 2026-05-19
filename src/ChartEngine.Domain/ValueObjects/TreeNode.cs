namespace ChartEngine.Domain.ValueObjects;

// Mirrors the JS tree node shape exactly.
// The JSON serializer will serialize this — property names must become camelCase.
// "childrenMap" is internal only — excluded from serialization.
public class TreeNode
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }

    // "value" in JS = the avg of the first metric at this node.
    // Computed during finalization.
    public double Value { get; set; }

    // Key = metric name (e.g. "Sales"), Value = aggregation results
    public Dictionary<string, MetricAggs> Aggs { get; set; } = new();

    // Output list — populated during finalization from ChildrenMap
    public List<TreeNode>? Children { get; set; }

    // Internal build structure — NOT serialized to JSON
    // Key = child node name (e.g. "North America")
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, TreeNode> ChildrenMap { get; set; } = new();
}
