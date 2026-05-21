using ChartEngine.Application.Interfaces.Visualization;

namespace ChartEngine.Infrastructure.Services.Visualization;

public class ChartFormatterRegistry
{
    private readonly Dictionary<string, IChartFormatter> _formatters;

    public ChartFormatterRegistry(IEnumerable<IChartFormatter> formatters)
    {
        _formatters = formatters.ToDictionary(f => f.ChartType.ToLowerInvariant());
    }

    public IChartFormatter GetFormatter(string chartType)
    {
        var key = string.IsNullOrWhiteSpace(chartType) ? "bar" : chartType.ToLowerInvariant();
        
        if (_formatters.TryGetValue(key, out var formatter))
        {
            return formatter;
        }
        
        // Fallback to bar chart if requested format doesn't exist
        return _formatters.TryGetValue("bar", out var defaultFormatter) 
            ? defaultFormatter 
            : throw new InvalidOperationException("Default bar chart formatter is missing.");
    }
}
