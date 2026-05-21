using ChartEngine.Application.DTOs;

namespace ChartEngine.Application.Interfaces.Services;

public interface IVisualizationService
{
    Task<ChartVisualizationDto> GetVisualizationAsync(Guid datasetId, string chartType, int drillDown, string aggregation);
}
