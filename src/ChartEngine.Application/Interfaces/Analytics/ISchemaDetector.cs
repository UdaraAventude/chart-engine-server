namespace ChartEngine.Application.Interfaces.Analytics;

using ChartEngine.Domain.ValueObjects;

public interface ISchemaDetector
{
    
    
    
    DatasetSchema Detect(
        IReadOnlyList<Dictionary<string, string>> sampleRows,
        int estimatedRowCount);
}

