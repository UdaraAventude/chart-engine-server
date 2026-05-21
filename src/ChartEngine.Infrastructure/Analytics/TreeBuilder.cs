namespace ChartEngine.Infrastructure.Analytics;

using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Domain.ValueObjects;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Collections.Generic;
using System.IO;

public class TreeBuilder : ITreeBuilder
{
    public async Task<(TreeNode Root, int TotalRows)> BuildAsync(
        string storagePath,
        DatasetSchema schema,
        Func<int, Task> onProgress,
        CancellationToken ct = default)
    {
        var root = new TreeNode { Name = "root", AggsArray = new MetricAggs[schema.Metrics.Count] };
        int totalRows = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim  // Let CsvHelper trim before allocating strings
        };

        using var reader = new StreamReader(storagePath, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 65536);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord!;

        int[] dimIndices = schema.Dimensions
            .Select(d => Array.FindIndex(headers, h => h.Equals(d, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        int[] metricIndices = schema.Metrics
            .Select(m => Array.FindIndex(headers, h => h.Equals(m, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var dimValues = new string[dimIndices.Length];
        var metricValues = new double?[metricIndices.Length];
        var stringPool = new Dictionary<string, string>(); // Pooling to drastically reduce GC pressure

        long fileSize = new FileInfo(storagePath).Length;
        long lastReportedPosition = 0;
        const int reportEveryNRows = 50_000;

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            // Dimensions: parse and pool
            for (int i = 0; i < dimIndices.Length; i++)
            {
                var raw = dimIndices[i] >= 0 ? csv.GetField(dimIndices[i]) : null;
                raw = raw ?? string.Empty;

                if (!stringPool.TryGetValue(raw, out var pooledStr))
                {
                    pooledStr = raw;
                    stringPool[raw] = pooledStr;
                }
                dimValues[i] = pooledStr;
            }

            // Metrics: fast double parse
            for (int i = 0; i < metricIndices.Length; i++)
            {
                var raw = metricIndices[i] >= 0 ? csv.GetField(metricIndices[i]) : null;
                metricValues[i] = raw != null && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
                    ? d : null;
            }

            AddRowToTree(root, dimValues, metricValues, schema.Config.MaxHierarchyDepth, metricIndices.Length);
            totalRows++;

            if (totalRows % reportEveryNRows == 0)
            {
                long currentPosition = reader.BaseStream.Position;
                if (currentPosition - lastReportedPosition > fileSize / 20)
                {
                    int percent = (int)((double)currentPosition / fileSize * 100);
                    // clamp inside local var to avoid math in every row
                    if (percent > 95) percent = 95;
                    await onProgress(percent);
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
        int maxDepth,
        int numMetrics)
    {
        var currentNode = root;
        int depthLimit = Math.Min(dimValues.Length, maxDepth);

        for (int i = 0; i < depthLimit; i++)
        {
            var value = dimValues[i];

            if (!currentNode.ChildrenMap.TryGetValue(value, out var childNode))
            {
                childNode = new TreeNode { Name = value, AggsArray = new MetricAggs[numMetrics] };
                currentNode.ChildrenMap[value] = childNode;
            }

            currentNode = childNode;
            UpdateAggregationsArray(currentNode, metricValues);
            currentNode.Count++;
        }

        UpdateAggregationsArray(root, metricValues);
        root.Count++;
    }

    private static void UpdateAggregationsArray(
        TreeNode node,
        double?[] metricValues)
    {
        for (int i = 0; i < metricValues.Length; i++)
        {
            if (metricValues[i] is double val)
            {
                if (node.AggsArray![i] == null)
                    node.AggsArray[i] = new MetricAggs();
                    
                node.AggsArray[i].Accumulate(val);
            }
        }
    }

    private static void FinalizeNode(TreeNode node, IReadOnlyList<string> metrics, bool isRoot = false)
    {
        // 1. Convert O(1) Array back to standard Dictionary
        if (node.AggsArray != null)
        {
            for (int i = 0; i < node.AggsArray.Length; i++)
            {
                if (node.AggsArray[i] != null)
                {
                    node.Aggs[metrics[i]] = node.AggsArray[i];
                }
            }
            // free up memory early
            node.AggsArray = null;
        }

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
