namespace ChartEngine.Domain.ValueObjects;

// The output of getDynamicConfig(rowCount) — controls how aggressive
// schema detection and tree building are for a given dataset size.
public record DynamicConfig(
    int MaxDimCardinality,   // max unique values for a column to be a Dimension
    int MaxHierarchyDepth,   // max drill levels in the tree
    int FallbackTopValues    // top-N values to keep if cardinality is too high
);
