using System.Text.Json;
using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;

namespace ChartEngine.Infrastructure.Services.Visualization;

public class VisualizationService : IVisualizationService
{
    private readonly ITreeRepository _treeRepository;
    private readonly ChartFormatterRegistry _registry;

    public VisualizationService(ITreeRepository treeRepository, ChartFormatterRegistry registry)
    {
        _treeRepository = treeRepository;
        _registry = registry;
    }

    public async Task<ChartVisualizationDto> GetVisualizationAsync(Guid datasetId, string chartType, int drillDown, string aggregation)
    {
        var treeEntity = await _treeRepository.GetByDatasetIdAsync(datasetId);
        if (treeEntity == null)
            throw new KeyNotFoundException($"Tree for dataset {datasetId} not found.");

        var treeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rootNode = JsonSerializer.Deserialize<TreeNode>(treeEntity.JsonData, treeOptions);
        
        if (rootNode == null)
            throw new InvalidOperationException("Failed to deserialize tree data.");

        // Navigate to drillDown level
        var targetNode = NavigateToLevel(rootNode, drillDown);

        var formatter = _registry.GetFormatter(chartType);
        
        return formatter.Format(targetNode, drillDown, aggregation);
    }

    private TreeNode NavigateToLevel(TreeNode current, int targetLevel)
    {
        // Simple BFS or level finding logic.
        // The tree aggregates from root (level 0).
        // Since we are formatting a specific level, we can just return the node at that level.
        // Typically drill downs apply to a specific path, but for simplicity here we assume
        // the client wants the current node representing that level. 
        // In a real scenario, drillDown would be a path. Here we just mock navigation.
        
        if (targetLevel <= 0 || current.Children == null || !current.Children.Any())
            return current;

        // Naive depth traversal for demo purposes
        var node = current;
        for (int i = 0; i < targetLevel; i++)
        {
            if (node.Children != null && node.Children.Any())
                node = node.Children.First().Value;
            else
                break;
        }

        return node;
    }
}
