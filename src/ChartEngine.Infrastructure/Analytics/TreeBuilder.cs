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
        var root = new TreeNode { Name = "root" };
        int totalRows = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.None  // skip trimming in hot loop — we trim below only for dimensions
        };

        using var reader = new StreamReader(storagePath, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord!;

        // --- PRE-RESOLVE INDICES (done ONCE, not per row) ---
        // This eliminates string key lookups inside the 2M-row hot loop.
        int[] dimIndices = schema.Dimensions
            .Select(d => Array.FindIndex(headers, h => h.Equals(d, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        int[] metricIndices = schema.Metrics
            .Select(m => Array.FindIndex(headers, h => h.Equals(m, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        // Reusable arrays — allocated once, overwritten each row (no GC pressure)
        var dimValues    = new string[dimIndices.Length];
        var metricValues = new double?[metricIndices.Length];

        long fileSize = new FileInfo(storagePath).Length;
        long lastReportedPosition = 0;
        const int reportEveryNRows = 50_000; // raised from 10k → less async overhead

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            // Read dimension values by index (fastest CsvHelper path)
            for (int i = 0; i < dimIndices.Length; i++)
            {
                var raw = dimIndices[i] >= 0 ? csv.GetField(dimIndices[i]) : null;
                dimValues[i] = raw?.Trim() ?? string.Empty;
            }

            // Read metric values by index
            for (int i = 0; i < metricIndices.Length; i++)
            {
                var raw = metricIndices[i] >= 0 ? csv.GetField(metricIndices[i]) : null;
                metricValues[i] = raw != null && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                    ? d : null;
            }

            AddRowToTree(root, dimValues, metricValues, schema);
            totalRows++;

            if (totalRows % reportEveryNRows == 0)
            {
                long currentPosition = reader.BaseStream.Position;
                if (currentPosition - lastReportedPosition > fileSize / 20)
                {
                    int percent = (int)((double)currentPosition / fileSize * 100);
                    await onProgress(Math.Clamp(percent, 0, 95));
                    lastReportedPosition = currentPosition;
                }
            }
        }

        await onProgress(96);
        FinalizeNode(root, schema.Metrics, isRoot: true);

        await onProgress(99);
        return (root, totalRows);
    }

    
    private static void AddRowToTree(
        TreeNode root,
        string[] dimValues,
        double?[] metricValues,
        DatasetSchema schema)
    {
        var currentNode = root;
        int depthLimit = Math.Min(dimValues.Length, schema.Config.MaxHierarchyDepth);

        // Walk down the tree up to the configured MaxHierarchyDepth
        for (int i = 0; i < depthLimit; i++)
        {
            var value = dimValues[i];

            if (!currentNode.ChildrenMap.TryGetValue(value, out var childNode))
            {
                childNode = new TreeNode { Name = value };
                currentNode.ChildrenMap[value] = childNode;
            }

            currentNode = childNode;
            UpdateAggregations(currentNode, metricValues, schema.Metrics);
            currentNode.Count++;
        }

        // Roll up aggregations to root
        UpdateAggregations(root, metricValues, schema.Metrics);
        root.Count++;
    }

    // Accepts pre-parsed double?[] — no string parsing or dictionary lookup here
    private static void UpdateAggregations(
        TreeNode node,
        double?[] metricValues,
        IReadOnlyList<string> metricNames)
    {
        for (int i = 0; i < metricValues.Length; i++)
        {
            if (metricValues[i] is double val)
            {
                if (!node.Aggs.TryGetValue(metricNames[i], out var agg))
                {
                    agg = new MetricAggs();
                    node.Aggs[metricNames[i]] = agg;
                }
                agg.Accumulate(val);
            }
        }
    }

    
    
    private static void FinalizeNode(TreeNode node, List<string> metrics, bool isRoot = false)
    {
        
        
        if (metrics.Count > 0 && node.Aggs.TryGetValue(metrics[0], out var primaryAgg))
        {
            node.Value = primaryAgg.Avg;
        }

        if (node.ChildrenMap.Count == 0)
        {
            
            node.Children = null;   
            return;
        }

        
        
        node.Children = node.ChildrenMap.Values
            .OrderByDescending(c => c.Count)
            .ToList();

        
        node.ChildrenMap.Clear();

        
        foreach (var child in node.Children)
        {
            FinalizeNode(child, metrics);
        }
    }
}

