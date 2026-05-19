namespace ChartEngine.Application.Interfaces.Analytics;

using ChartEngine.Domain.ValueObjects;

public interface ISchemaDetector
{
    // Takes a sample of rows (up to 500) and the estimated total row count.
    // Returns the classified schema.
    // This is a pure, synchronous operation — no I/O, no DB.
    DatasetSchema Detect(
        IReadOnlyList<Dictionary<string, string>> sampleRows,
        int estimatedRowCount);
}
