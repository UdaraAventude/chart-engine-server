using ChartEngine.Application.DTOs;
using ChartEngine.Domain.ValueObjects;
using System.Text.Json;

namespace ChartEngine.Infrastructure.Analytics;

public static class TreeNavigator
{
    public static TreeNode NavigateToPath(TreeNode root, IReadOnlyList<DrillPathStepDto> path)
    {
        var node = root;
        foreach (var step in path)
        {
            if (step.Column.StartsWith("__hist__", StringComparison.Ordinal))
                continue;

            if (node.Children == null || node.Children.Count == 0)
                break;

            var next = node.Children.FirstOrDefault(c => c.Name == step.Value);
            if (next == null)
                break;

            node = next;
        }

        return node;
    }

    public static IReadOnlyList<DrillPathStepDto> ParseDrillPathJson(string? drillPathJson)
    {
        if (string.IsNullOrWhiteSpace(drillPathJson))
            return Array.Empty<DrillPathStepDto>();

        try
        {
            var steps = JsonSerializer.Deserialize<List<DrillPathStepDto>>(
                drillPathJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return steps ?? new List<DrillPathStepDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<DrillPathStepDto>();
        }
    }

    /// <summary>Legacy depth navigation (first child at each level).</summary>
    public static TreeNode NavigateToLevel(TreeNode current, int targetLevel)
    {
        if (targetLevel <= 0 || current.Children == null || current.Children.Count == 0)
            return current;

        var node = current;
        for (int i = 0; i < targetLevel; i++)
        {
            if (node.Children != null && node.Children.Count > 0)
                node = node.Children[0];
            else
                break;
        }

        return node;
    }
}
