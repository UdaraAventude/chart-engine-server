namespace ChartEngine.Domain.ValueObjects;




public class TreeNode
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }

    
    
    public double Value { get; set; }

    
    public Dictionary<string, MetricAggs> Aggs { get; set; } = new();

    
    public List<TreeNode>? Children { get; set; }

    
    
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, TreeNode> ChildrenMap { get; set; } = new();
}

