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
}

