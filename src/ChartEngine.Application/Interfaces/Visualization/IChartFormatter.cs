using ChartEngine.Application.DTOs;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;

namespace ChartEngine.Application.Interfaces.Visualization;

public interface IChartFormatter
{
    string ChartType { get; }
    
    ChartVisualizationDto Format(TreeNode node, int level, string groupBy);
}
