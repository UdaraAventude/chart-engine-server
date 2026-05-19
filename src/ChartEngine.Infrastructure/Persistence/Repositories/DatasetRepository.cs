namespace ChartEngine.Infrastructure.Persistence.Repositories;

using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;



public class DatasetRepository : IDatasetRepository
{
    private readonly AppDbContext _context;

    
    public DatasetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        
        
        return await _context.Datasets.FindAsync([id], ct);
    }

    public async Task AddAsync(Dataset dataset, CancellationToken ct = default)
    {
        await _context.Datasets.AddAsync(dataset, ct);
        await _context.SaveChangesAsync(ct);  
    }

    public async Task UpdateAsync(Dataset dataset, CancellationToken ct = default)
    {
        
        
        _context.Datasets.Update(dataset);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SaveColumnsAsync(IEnumerable<DatasetColumn> columns, CancellationToken ct = default)
    {
        await _context.DatasetColumns.AddRangeAsync(columns, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<DatasetColumn>> GetColumnsAsync(Guid datasetId, CancellationToken ct = default)
    {
        return await _context.DatasetColumns
            .Where(c => c.DatasetId == datasetId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }

    public async Task SaveConfigAsync(DatasetConfig config, CancellationToken ct = default)
    {
        await _context.DatasetConfigs.AddAsync(config, ct);
        await _context.SaveChangesAsync(ct);
    }
}

