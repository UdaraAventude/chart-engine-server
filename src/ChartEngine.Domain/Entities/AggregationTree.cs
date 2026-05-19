namespace ChartEngine.Domain.Entities;

public class AggregationTree
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DatasetId { get; private set; }
    public string TreeJson { get; private set; } = string.Empty;
    public DateTime ComputedAt { get; private set; } = DateTime.UtcNow;

    private AggregationTree() { }

    public static AggregationTree Create(Guid datasetId, string treeJson)
        => new() { DatasetId = datasetId, TreeJson = treeJson };
}
