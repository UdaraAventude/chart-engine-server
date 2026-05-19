namespace ChartEngine.Infrastructure.Persistence.Repositories;

using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

// This class implements the IDatasetRepository interface from Application.
// The Application layer defined the contract. We fulfill it here with EF Core.
public class DatasetRepository : IDatasetRepository
{
    private readonly AppDbContext _context;

    // AppDbContext is injected — we don't create it, the DI container gives it to us
    public DatasetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // FindAsync is EF Core's way to find by primary key
        // Returns null if not found — caller decides what to do with null
        return await _context.Datasets.FindAsync([id], ct);
    }

    public async Task AddAsync(Dataset dataset, CancellationToken ct = default)
    {
        await _context.Datasets.AddAsync(dataset, ct);
        await _context.SaveChangesAsync(ct);  // This executes the INSERT SQL
    }

    public async Task UpdateAsync(Dataset dataset, CancellationToken ct = default)
    {
        // EF Core tracks changes automatically — if you loaded the entity and modified it,
        // Update() marks all changes as modified and SaveChanges() runs the UPDATE SQL
        _context.Datasets.Update(dataset);
        await _context.SaveChangesAsync(ct);
    }
}
