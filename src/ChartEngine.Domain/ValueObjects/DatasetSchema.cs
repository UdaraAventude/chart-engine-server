namespace ChartEngine.Domain.ValueObjects;

// The complete schema for a dataset — this is what the pipeline passes
// from SchemaDetector to TreeBuilder.
public record DatasetSchema(
    List<string> Dimensions,   // column names classified as dimensions (in order)
    List<string> Metrics,      // column names classified as metrics (in order)
    List<DetectedColumn> Rejected,  // columns that were rejected and why
    DynamicConfig Config
);
