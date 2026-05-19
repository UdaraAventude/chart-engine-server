namespace ChartEngine.Infrastructure.Analytics;

using ChartEngine.Domain.ValueObjects;



public static class DynamicConfigResolver
{
    public static DynamicConfig Resolve(int estimatedRowCount)
    {
        
        
        
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

            _ => new DynamicConfig(                
                MaxDimCardinality: 500,
                MaxHierarchyDepth: 7,
                FallbackTopValues: 20)
        };
    }
}

