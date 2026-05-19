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
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(storagePath);
        using var csv = new CsvReader(reader, config);

        
        await csv.ReadAsync();
        csv.ReadHeader();

        
        
        long fileSize = new FileInfo(storagePath).Length;
        long lastReportedPosition = 0;
        int reportEveryNRows = 10_000;  

        
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in csv.HeaderRecord!)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            
            AddRowToTree(root, row, schema);
            totalRows++;

            
            if (totalRows % reportEveryNRows == 0)
            {
                long currentPosition = reader.BaseStream.Position;
                int percent = (int)((double)currentPosition / fileSize * 100);
                percent = Math.Clamp(percent, 0, 95);  

                if (currentPosition - lastReportedPosition > fileSize / 20)  
                {
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
        Dictionary<string, string> row,
        DatasetSchema schema)
    {
        var currentNode = root;

        
        
        foreach (var dimension in schema.Dimensions)
        {
            var value = row.TryGetValue(dimension, out var v)
                ? (v?.Trim() ?? string.Empty)
                : string.Empty;

            
            if (!currentNode.ChildrenMap.TryGetValue(value, out var childNode))
            {
                childNode = new TreeNode { Name = value };
                currentNode.ChildrenMap[value] = childNode;
            }

            
            currentNode = childNode;

            
            UpdateAggregations(currentNode, row, schema.Metrics);
            currentNode.Count++;
        }

        
        
        UpdateAggregations(root, row, schema.Metrics);
        root.Count++;
    }

    
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

