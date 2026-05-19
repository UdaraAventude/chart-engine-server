namespace ChartEngine.Infrastructure.Analytics;

using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Domain.ValueObjects;

public class SchemaDetector : ISchemaDetector
{
    
    
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
        "MM-dd-yyyy", "dd-MM-yyyy", "yyyy/MM/dd"
    ];

    public DatasetSchema Detect(
        IReadOnlyList<Dictionary<string, string>> sampleRows,
        int estimatedRowCount)
    {
        
        var config = DynamicConfigResolver.Resolve(estimatedRowCount);

        if (sampleRows.Count == 0)
            return new DatasetSchema([], [], [], config);

        
        var allColumns = sampleRows[0].Keys.ToList();

        
        var dimensions = new List<string>();
        var metrics = new List<string>();
        var rejected = new List<DetectedColumn>();

        foreach (var column in allColumns)
        {
            var classification = ClassifyColumn(column, sampleRows, config);

            switch (classification.Classification)
            {
                case ColumnClassification.Dimension:
                    dimensions.Add(column);
                    break;
                case ColumnClassification.Metric:
                    metrics.Add(column);
                    break;
                case ColumnClassification.Rejected:
                    rejected.Add(classification);
                    break;
            }
        }

        
        
        
        if (dimensions.Count == 0 && allColumns.Count > 0)
        {
            var firstColumn = allColumns[0];
            metrics.Remove(firstColumn);
            var existing = rejected.FirstOrDefault(r => r.Name == firstColumn);
            if (existing != null) rejected.Remove(existing);
            dimensions.Add(firstColumn);
        }

        return new DatasetSchema(dimensions, metrics, rejected, config);
    }

    private DetectedColumn ClassifyColumn(
        string columnName,
        IReadOnlyList<Dictionary<string, string>> sampleRows,
        DynamicConfig config)
    {
        
        var values = sampleRows
            .Select(row => row.TryGetValue(columnName, out var v) ? v?.Trim() : null)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        if (values.Count == 0)
        {
            
            return new DetectedColumn(columnName, ColumnClassification.Rejected,
                Cardinality: 0, RejectionReason: "empty_column");
        }

        
        
        
        
        int dateParseCount = values.Count(v => v != null && IsDateValue(v));
        if (dateParseCount > values.Count * 0.8)
        {
            return new DetectedColumn(columnName, ColumnClassification.Rejected,
                Cardinality: 0, RejectionReason: "date_column");
        }

        
        
        bool allNumeric = values.All(v => double.TryParse(v,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out _));

        if (allNumeric)
        {
            return new DetectedColumn(columnName, ColumnClassification.Metric, Cardinality: 0);
        }

        
        
        
        int uniqueCount = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        if (uniqueCount > config.MaxDimCardinality)
        {
            return new DetectedColumn(columnName, ColumnClassification.Rejected,
                Cardinality: uniqueCount, RejectionReason: "too_many_unique");
        }

        
        return new DetectedColumn(columnName, ColumnClassification.Dimension,
            Cardinality: uniqueCount);
    }

    private static bool IsDateValue(string value)
    {
        return DateFormats.Any(format =>
            DateTime.TryParseExact(
                value, format,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _));
    }
}

