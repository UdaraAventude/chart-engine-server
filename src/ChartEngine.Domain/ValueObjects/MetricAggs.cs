namespace ChartEngine.Domain.ValueObjects;

// Mirrors the JS aggs object: { avg, sum, min, max, count }
// Note: Avg is computed at finalization — during building we only track sum/min/max/count.
public class MetricAggs
{
    public double Sum { get; set; }
    public double Min { get; set; } = double.MaxValue;
    public double Max { get; set; } = double.MinValue;
    public int Count { get; set; }

    // Avg is computed — not stored separately during building.
    // This property is included in JSON serialization.
    public double Avg => Count > 0 ? Sum / Count : 0;

    public void Accumulate(double value)
    {
        Sum += value;
        Count++;
        if (value < Min) Min = value;
        if (value > Max) Max = value;
    }
}
