namespace ChartEngine.Infrastructure.Analytics;

using ChartEngine.Domain.ValueObjects;

// Ports getDynamicConfig(rowCount) from global-formatter/index.js exactly.
// Determines how aggressively we classify columns based on dataset size.
public static class DynamicConfigResolver
{
    public static DynamicConfig Resolve(int estimatedRowCount)
    {
        // These thresholds are copied directly from the JS — do not change them.
        // Changing them would cause the C# tree to differ from what the JS produced,
        // which would break datasets that were already processed client-side.
        return estimatedRowCount switch
        {
            > 1_000_000 => new DynamicConfig(
                MaxDimCardinality: 50,
                MaxHierarchyDepth: 4,
                FallbackTopValues: 10),

            > 100_000 => new DynamicConfig(
                MaxDimCardinality: 100,
                MaxHierarchyDepth: 5,
                FallbackTopValues: 15),

            > 10_000 => new DynamicConfig(
                MaxDimCardinality: 200,
                MaxHierarchyDepth: 6,
                FallbackTopValues: 20),

            _ => new DynamicConfig(                // ≤ 10,000 rows
                MaxDimCardinality: 500,
                MaxHierarchyDepth: 7,
                FallbackTopValues: 20)
        };
    }
}
