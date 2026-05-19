namespace ChartEngine.Domain.ValueObjects;



public record DatasetSchema(
    List<string> Dimensions,   
    List<string> Metrics,      
    List<DetectedColumn> Rejected,  
    DynamicConfig Config
);

