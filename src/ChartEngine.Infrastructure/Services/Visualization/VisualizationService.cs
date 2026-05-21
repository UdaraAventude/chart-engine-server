using System.Text.Json;
using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Analytics;

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

    public async Task<ChartVisualizationDto> GetVisualizationAsync(
        Guid datasetId,
        string chartType,
        int drillDown,
        string aggregation,
        string? drillPathJson = null)
    {
        var jsonTree = await _treeRepository.GetTreeJsonAsync(datasetId);
        if (string.IsNullOrEmpty(jsonTree))
            throw new KeyNotFoundException($"Tree for dataset {datasetId} not found.");

        var treeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        TreeNode? rootNode;
        string groupedBy = aggregation;
        using (var doc = JsonDocument.Parse(jsonTree))
        {
            var root = doc.RootElement;
            var treeElement = root.TryGetProperty("tree", out var t) ? t : root;
            rootNode = JsonSerializer.Deserialize<TreeNode>(treeElement.GetRawText(), treeOptions);

            if (root.TryGetProperty("dimensions", out var dims) && dims.ValueKind == JsonValueKind.Array)
            {
                var dimList = dims.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
                var pathSteps = TreeNavigator.ParseDrillPathJson(drillPathJson);
                var depth = pathSteps.Count > 0 ? pathSteps.Count : drillDown;
                if (depth < dimList.Count)
                    groupedBy = dimList[depth];
            }
        }

        if (rootNode == null)
            throw new InvalidOperationException("Failed to deserialize tree data.");

        var path = TreeNavigator.ParseDrillPathJson(drillPathJson);
        var targetNode = path.Count > 0
            ? TreeNavigator.NavigateToPath(rootNode, path)
            : TreeNavigator.NavigateToLevel(rootNode, drillDown);

        var effectiveLevel = path.Count > 0 ? path.Count : drillDown;
        var formatter = _registry.GetFormatter(chartType);

        return formatter.Format(targetNode, effectiveLevel, groupedBy);
    }
}
