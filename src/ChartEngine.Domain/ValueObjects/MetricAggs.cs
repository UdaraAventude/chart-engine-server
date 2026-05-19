namespace ChartEngine.Domain.ValueObjects;



public class MetricAggs
{
    public double Sum { get; set; }
    public double Min { get; set; } = double.MaxValue;
    public double Max { get; set; } = double.MinValue;
    public int Count { get; set; }

    
    
    public double Avg => Count > 0 ? Sum / Count : 0;

    public void Accumulate(double value)
    {
        Sum += value;
        Count++;
        if (value < Min) Min = value;
        if (value > Max) Max = value;
    }
}

