namespace ChartEngine.Infrastructure.Persistence.Repositories;

using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
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

        
        var existing = await _context.AggregationTrees
            .FirstOrDefaultAsync(t => t.DatasetId == datasetId, ct);

        if (existing is not null)
        {
            
            _context.AggregationTrees.Remove(existing);
        }

        var tree = AggregationTree.Create(datasetId, json);
        await _context.AggregationTrees.AddAsync(tree, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<string?> GetTreeJsonAsync(Guid datasetId, CancellationToken ct = default)
    {
        var tree = await _context.AggregationTrees
            .AsNoTracking()   
            .FirstOrDefaultAsync(t => t.DatasetId == datasetId, ct);

        return tree?.TreeJson;
    }
}

