namespace ChartEngine.Infrastructure.Analytics;

using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Domain.ValueObjects;

public class SchemaDetector : ISchemaDetector
{
    // These date patterns mirror the JS preprocessDates() detection.
    // Order matters: more specific patterns first.
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
        // Step 1: Get the config for this dataset size
        var config = DynamicConfigResolver.Resolve(estimatedRowCount);

        if (sampleRows.Count == 0)
            return new DatasetSchema([], [], [], config);

        // Step 2: Get all column names from the first row
        var allColumns = sampleRows[0].Keys.ToList();

        // Step 3: Classify each column
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

        // Step 4: If we found no dimensions at all, fall back:
        // take the first string column and force it to be a dimension.
        // This mirrors the JS fallback bucketing logic.
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
        // Collect all non-empty values for this column from the sample
        var values = sampleRows
            .Select(row => row.TryGetValue(columnName, out var v) ? v?.Trim() : null)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        if (values.Count == 0)
        {
            // Empty column — reject it, nothing to work with
            return new DetectedColumn(columnName, ColumnClassification.Rejected,
                Cardinality: 0, RejectionReason: "empty_column");
        }

        // --- Test 1: Is it a date column? ---
        // Check if a majority of the sampled values parse as dates.
        // We use "majority" (> 80%) rather than "all" to handle sparse or
        // mixed-format columns gracefully, just as the JS does.
        int dateParseCount = values.Count(v => v != null && IsDateValue(v));
        if (dateParseCount > values.Count * 0.8)
        {
            return new DetectedColumn(columnName, ColumnClassification.Rejected,
                Cardinality: 0, RejectionReason: "date_column");
        }

        // --- Test 2: Is it a numeric column (metric)? ---
        // If ALL non-empty values parse as numbers, it's a metric.
        bool allNumeric = values.All(v => double.TryParse(v,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out _));

        if (allNumeric)
        {
            return new DetectedColumn(columnName, ColumnClassification.Metric, Cardinality: 0);
        }

        // --- Test 3: Is cardinality too high for a dimension? ---
        // Count unique values in the sample. If there are too many,
        // it's something like an ID column — useless as a drill dimension.
        int uniqueCount = values.Distinct(StringComparer.OrdinalIgnoreCase).Count();

        if (uniqueCount > config.MaxDimCardinality)
        {
            return new DetectedColumn(columnName, ColumnClassification.Rejected,
                Cardinality: uniqueCount, RejectionReason: "too_many_unique");
        }

        // --- Default: it's a dimension ---
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
