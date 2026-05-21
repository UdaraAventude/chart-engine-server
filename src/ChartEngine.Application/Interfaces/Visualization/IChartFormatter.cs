using ChartEngine.Application.DTOs;
using ChartEngine.Domain.Entities;

namespace ChartEngine.Application.Interfaces.Visualization;

public interface IChartFormatter
{
    string ChartType { get; }
    
    ChartVisualizationDto Format(TreeNode node, int level, string groupBy);
}
