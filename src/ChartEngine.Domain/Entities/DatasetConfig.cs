namespace ChartEngine.Domain.Entities;

// Mirrors getDynamicConfig() output — stored per dataset so we know
// what cardinality limits were applied when the tree was built.
public class DatasetConfig
{
    public Guid DatasetId { get; private set; }
    public int MaxDimCardinality { get; private set; }
    public int MaxHierarchyDepth { get; private set; }
    public int FallbackTopValues { get; private set; }

    private DatasetConfig() { }

    public static DatasetConfig Create(Guid datasetId, int maxDimCardinality, int maxHierarchyDepth, int fallbackTopValues)
        => new()
        {
            DatasetId = datasetId,
            MaxDimCardinality = maxDimCardinality,
            MaxHierarchyDepth = maxHierarchyDepth,
            FallbackTopValues = fallbackTopValues
        };
}
