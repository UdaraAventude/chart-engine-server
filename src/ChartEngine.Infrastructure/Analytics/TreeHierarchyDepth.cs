using System.Text.Json;

namespace ChartEngine.Infrastructure.Analytics;

public static class TreeHierarchyDepth
{
    /// <summary>
    /// Deepest path from root through child nodes (number of dimension levels in the stored tree).
    /// </summary>
    public static int ComputeFromTreeElement(JsonElement treeNode)
    {
        if (treeNode.ValueKind != JsonValueKind.Object)
            return 0;

        if (!TryGetChildren(treeNode, out var children) || children.Count == 0)
            return 0;

        var maxBelow = 0;
        foreach (var child in children)
        {
            var depth = 1 + ComputeFromTreeElement(child);
            if (depth > maxBelow)
                maxBelow = depth;
        }

        return maxBelow;
    }

    private static bool TryGetChildren(JsonElement node, out List<JsonElement> children)
    {
        children = new List<JsonElement>();

        if (node.TryGetProperty("children", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in arr.EnumerateArray())
                children.Add(c);
        }
        else if (node.TryGetProperty("Children", out var arr2) && arr2.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in arr2.EnumerateArray())
                children.Add(c);
        }

        return children.Count > 0;
    }
}
