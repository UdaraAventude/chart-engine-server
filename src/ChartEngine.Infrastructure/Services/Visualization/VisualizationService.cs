using System.Text.Json;
using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;

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
        var jsonTree = await _treeRepository.GetTreeJsonAsync(datasetId);
        if (string.IsNullOrEmpty(jsonTree))
            throw new KeyNotFoundException($"Tree for dataset {datasetId} not found.");

        var treeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // The repository persists a wrapper envelope: { tree, dimensions, metrics, rejected, totalRows }.
        // Extract the nested "tree" element before deserializing as TreeNode.
        TreeNode? rootNode;
        using (var doc = JsonDocument.Parse(jsonTree))
        {
            var root = doc.RootElement;
            var treeElement = root.TryGetProperty("tree", out var t) ? t : root;
            rootNode = JsonSerializer.Deserialize<TreeNode>(treeElement.GetRawText(), treeOptions);
        }

        if (rootNode == null)
            throw new InvalidOperationException("Failed to deserialize tree data.");

        // Navigate to drillDown level
        var targetNode = NavigateToLevel(rootNode, drillDown);

        var formatter = _registry.GetFormatter(chartType);
        
        return formatter.Format(targetNode, drillDown, aggregation);
    }

    private TreeNode NavigateToLevel(TreeNode current, int targetLevel)
    {
        if (targetLevel <= 0 || current.Children == null || !current.Children.Any())
            return current;

        // Naive depth traversal for demo purposes
        var node = current;
        for (int i = 0; i < targetLevel; i++)
        {
            if (node.Children != null && node.Children.Any())
                node = node.Children.First();
            else
                break;
        }

        return node;
    }
}
