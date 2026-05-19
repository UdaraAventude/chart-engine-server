namespace ChartEngine.Domain.ValueObjects;

// A single column classification result — produced by SchemaDetector,
// consumed by TreeBuilder to know which columns to use for hierarchy vs metrics.
public record DetectedColumn(
    string Name,
    ColumnClassification Classification,
    int Cardinality,
    string? RejectionReason = null
);

public enum ColumnClassification { Dimension, Metric, Rejected }
