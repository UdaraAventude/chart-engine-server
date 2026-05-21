namespace ChartEngine.Infrastructure.Persistence.Repositories;

using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Analytics;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class TreeRepository : ITreeRepository
{
    private readonly AppDbContext _context;

    
    
    
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public TreeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(
        Guid datasetId,
        TreeNode root,
        DatasetSchema schema,
        int totalRows,
        CancellationToken ct = default)
    {
        var treeData = new
        {
            tree = root,
            dimensions = schema.Dimensions,
            metrics = schema.Metrics,
            maxHierarchyDepth = schema.Config.MaxHierarchyDepth,
            rejected = schema.Rejected.Select(r => new
            {
                key = r.Name,
                reason = r.RejectionReason,
                cardinality = r.Cardinality
            }),
            totalRows
        };

        // Stream JSON directly into GZipStream (zero-allocation for the massive string/byte[])
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
        {
            await JsonSerializer.SerializeAsync(gzipStream, treeData, SerializerOptions, ct);
        }
        
        var compressedJson = Convert.ToBase64String(memoryStream.ToArray());

        var existing = await _context.AggregationTrees
            .FirstOrDefaultAsync(t => t.DatasetId == datasetId, ct);

        if (existing is not null)
        {
            _context.AggregationTrees.Remove(existing);
        }

        var tree = AggregationTree.Create(datasetId, compressedJson);
        await _context.AggregationTrees.AddAsync(tree, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<string?> GetTreeJsonAsync(Guid datasetId, CancellationToken ct = default)
    {
        var tree = await _context.AggregationTrees
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.DatasetId == datasetId, ct);

        if (tree is null)
            return null;

        return Decompress(tree.TreeJson);
    }

    public async Task<TreeEnvelopeMetadata?> GetEnvelopeMetadataAsync(Guid datasetId, CancellationToken ct = default)
    {
        var json = await GetTreeJsonAsync(datasetId, ct);
        if (string.IsNullOrEmpty(json))
            return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var dimensions = ReadStringArray(root, "dimensions");
        var metrics = ReadStringArray(root, "metrics");
        var rejected = ReadRejected(root);
        var totalRows = root.TryGetProperty("totalRows", out var tr) ? tr.GetInt32() : 0;

        var maxHierarchyDepth = 0;
        if (root.TryGetProperty("maxHierarchyDepth", out var md) && md.GetInt32() > 0)
            maxHierarchyDepth = md.GetInt32();

        if (maxHierarchyDepth <= 0 || maxHierarchyDepth > dimensions.Count)
        {
            var treeElement = root.TryGetProperty("tree", out var t) ? t : root;
            var computed = TreeHierarchyDepth.ComputeFromTreeElement(treeElement);
            if (computed > 0)
                maxHierarchyDepth = computed;
        }

        if (maxHierarchyDepth <= 0)
            maxHierarchyDepth = Math.Min(dimensions.Count, totalRows > 1_000_000 ? 4 : dimensions.Count);

        return new TreeEnvelopeMetadata(dimensions, metrics, rejected, totalRows, maxHierarchyDepth);
    }

    private static List<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return arr.EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static List<RejectedColumnDto> ReadRejected(JsonElement root)
    {
        if (!root.TryGetProperty("rejected", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<RejectedColumnDto>();

        var list = new List<RejectedColumnDto>();
        foreach (var item in arr.EnumerateArray())
        {
            var key = item.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
            var reason = item.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
            var cardinality = item.TryGetProperty("cardinality", out var c) ? c.GetInt32() : 0;
            if (!string.IsNullOrEmpty(key))
                list.Add(new RejectedColumnDto(key, reason, cardinality));
        }

        return list;
    }

    private static string Decompress(string compressedText)
    {
        if (string.IsNullOrWhiteSpace(compressedText)) return compressedText;

        string trimmed = compressedText.TrimStart();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            return compressedText;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(compressedText);
            using var inputStream = new MemoryStream(bytes);
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return compressedText;
        }
    }
}

