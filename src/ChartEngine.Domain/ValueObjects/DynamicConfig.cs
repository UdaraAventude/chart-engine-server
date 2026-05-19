namespace ChartEngine.Domain.ValueObjects;



public record DynamicConfig(
    int MaxDimCardinality,   
    int MaxHierarchyDepth,   
    int FallbackTopValues    
);

