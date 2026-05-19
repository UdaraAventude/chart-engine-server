namespace ChartEngine.Domain.ValueObjects;



public record DetectedColumn(
    string Name,
    ColumnClassification Classification,
    int Cardinality,
    string? RejectionReason = null
);

public enum ColumnClassification { Dimension, Metric, Rejected }

