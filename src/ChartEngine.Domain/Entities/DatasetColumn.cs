namespace ChartEngine.Domain.Entities;



public class DatasetColumn
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DatasetId { get; private set; }
    public string ColumnName { get; private set; } = string.Empty;
    public ColumnRole Role { get; private set; }  
    public int? Cardinality { get; private set; }
    public string? RejectionReason { get; private set; }
    public int SortOrder { get; private set; }

    private DatasetColumn() { }

    public static DatasetColumn AsDimension(Guid datasetId, string name, int cardinality, int order)
        => new() { DatasetId = datasetId, ColumnName = name, Role = ColumnRole.Dimension, Cardinality = cardinality, SortOrder = order };

    public static DatasetColumn AsMetric(Guid datasetId, string name, int order)
        => new() { DatasetId = datasetId, ColumnName = name, Role = ColumnRole.Metric, SortOrder = order };

    public static DatasetColumn AsRejected(Guid datasetId, string name, string reason, int cardinality, int order)
        => new() { DatasetId = datasetId, ColumnName = name, Role = ColumnRole.Rejected, RejectionReason = reason, Cardinality = cardinality, SortOrder = order };
}

public enum ColumnRole { Dimension, Metric, Rejected }

