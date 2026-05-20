using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.Infrastructure.Persistence.Repositories;

public class ExportRepository : IExportRepository
{
    private readonly AppDbContext _context;

    public ExportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ExportJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ExportJobs.FindAsync([id], ct);
    }

    public async Task AddAsync(ExportJob job, CancellationToken ct = default)
    {
        await _context.ExportJobs.AddAsync(job, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ExportJob job, CancellationToken ct = default)
    {
        _context.ExportJobs.Update(job);
        await _context.SaveChangesAsync(ct);
    }
}
