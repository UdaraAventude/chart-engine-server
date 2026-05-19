namespace ChartEngine.Infrastructure.Analytics;

using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Domain.ValueObjects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

public class TreeBuilder : ITreeBuilder
{
    public async Task<(TreeNode Root, int TotalRows)> BuildAsync(
        string storagePath,
        DatasetSchema schema,
        Func<int, Task> onProgress,
        CancellationToken ct = default)
    {
        // Create the root node — this is the entry point of the entire tree.
        // "root" is the name used by the JS, we keep it identical.
        var root = new TreeNode { Name = "root" };
        int totalRows = 0;

        // CsvHelper streams the file — it never loads everything into memory at once.
        // This is the C# equivalent of PapaParse's { chunk: ... } streaming mode.
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,    // ignore missing fields gracefully
            BadDataFound = null,         // skip bad rows rather than throwing
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(storagePath);
        using var csv = new CsvReader(reader, config);

        // Read the header row
        await csv.ReadAsync();
        csv.ReadHeader();

        // We'll report progress based on file position.
        // Get total file size for percentage calculation.
        long fileSize = new FileInfo(storagePath).Length;
        long lastReportedPosition = 0;
        int reportEveryNRows = 10_000;  // report progress every 10k rows

        // Process rows one at a time — never accumulate all rows in memory
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            // Read this row as a flat dictionary: { "Region": "North", "Sales": "100.5", ... }
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in csv.HeaderRecord!)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            // Add this row to the tree — this is the core operation
            AddRowToTree(root, row, schema);
            totalRows++;

            // Report progress periodically
            if (totalRows % reportEveryNRows == 0)
            {
                long currentPosition = reader.BaseStream.Position;
                int percent = (int)((double)currentPosition / fileSize * 100);
                percent = Math.Clamp(percent, 0, 95);  // cap at 95 — finalization is the last 5%

                if (currentPosition - lastReportedPosition > fileSize / 20)  // report at most 20 times
                {
                    await onProgress(percent);
                    lastReportedPosition = currentPosition;
                }
            }
        }

        // All rows processed. Now finalize the tree:
        // compute averages, convert childrenMaps to sorted children arrays.
        await onProgress(96);
        FinalizeNode(root, schema.Metrics, isRoot: true);

        await onProgress(99);
        return (root, totalRows);
    }

    // --- Core tree building: called once per row ---
    private static void AddRowToTree(
        TreeNode root,
        Dictionary<string, string> row,
        DatasetSchema schema)
    {
        var currentNode = root;

        // Walk through each dimension in the schema's order.
        // For each dimension, find or create a child node for this row's value.
        foreach (var dimension in schema.Dimensions)
        {
            var value = row.TryGetValue(dimension, out var v)
                ? (v?.Trim() ?? string.Empty)
                : string.Empty;

            // Find or create the child node for this value
            if (!currentNode.ChildrenMap.TryGetValue(value, out var childNode))
            {
                childNode = new TreeNode { Name = value };
                currentNode.ChildrenMap[value] = childNode;
            }

            // Move into the child
            currentNode = childNode;

            // Update aggregations at this node for every metric
            UpdateAggregations(currentNode, row, schema.Metrics);
            currentNode.Count++;
        }

        // Also update the root node's aggregations
        // (root represents the entire dataset)
        UpdateAggregations(root, row, schema.Metrics);
        root.Count++;
    }

    // Accumulate metric values at a node
    private static void UpdateAggregations(
        TreeNode node,
        Dictionary<string, string> row,
        List<string> metrics)
    {
        foreach (var metric in metrics)
        {
            if (!node.Aggs.TryGetValue(metric, out var agg))
            {
                agg = new MetricAggs();
                node.Aggs[metric] = agg;
            }

            if (row.TryGetValue(metric, out var rawValue) &&
                double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
            {
                agg.Accumulate(numericValue);
            }
        }
    }

    // --- Finalization: called once after all rows are processed ---
    // Recursively converts ChildrenMap → Children[] and computes the "value" field.
    private static void FinalizeNode(TreeNode node, List<string> metrics, bool isRoot = false)
    {
        // Set the node's primary "value" — the avg of the first metric.
        // This mirrors the JS finalizeTree() behavior.
        if (metrics.Count > 0 && node.Aggs.TryGetValue(metrics[0], out var primaryAgg))
        {
            node.Value = primaryAgg.Avg;
        }

        if (node.ChildrenMap.Count == 0)
        {
            // Leaf node — no children to process
            node.Children = null;   // null = leaf, [] would also be valid but null is cleaner
            return;
        }

        // Convert the internal ChildrenMap to the output Children list.
        // Sort by count descending — highest volume nodes first, matching JS behavior.
        node.Children = node.ChildrenMap.Values
            .OrderByDescending(c => c.Count)
            .ToList();

        // Clear the map — we no longer need it and it wastes memory
        node.ChildrenMap.Clear();

        // Recurse into all children
        foreach (var child in node.Children)
        {
            FinalizeNode(child, metrics);
        }
    }
}
