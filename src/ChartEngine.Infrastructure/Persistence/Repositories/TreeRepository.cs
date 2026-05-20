namespace ChartEngine.Infrastructure.Persistence.Repositories;

using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;
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
            rejected = schema.Rejected.Select(r => new
            {
                key = r.Name,
                reason = r.RejectionReason,
                cardinality = r.Cardinality
            }),
            totalRows
        };

        var json = JsonSerializer.Serialize(treeData, SerializerOptions);
        var compressedJson = Compress(json);

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

    private static string Compress(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, leaveOpen: true))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        return Convert.ToBase64String(outputStream.ToArray());
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

