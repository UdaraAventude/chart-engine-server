namespace ChartEngine.Infrastructure.Persistence.Repositories;

using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task<PagedListDto<DatasetListDto>> GetPagedDatasetsAsync(
        int page,
        int pageSize,
        string? sortBy,
        string? search,
        CancellationToken ct = default)
    {
        var query = _context.Datasets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => d.FileName.Contains(search));
        }

        if (string.Equals(sortBy, "fileName", StringComparison.OrdinalIgnoreCase))
        {
            query = query.OrderBy(d => d.FileName);
        }
        else
        {
            // Default to sorting by CreatedAt descending
            query = query.OrderByDescending(d => d.CreatedAt);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DatasetListDto(
                d.Id,
                d.FileName,
                d.Status.ToString(),
                d.TotalRows,
                d.CreatedAt
            ))
            .ToListAsync(ct);

        return new PagedListDto<DatasetListDto>(items, totalCount, page, pageSize);
    }

    public async Task DeleteAsync(Dataset dataset, CancellationToken ct = default)
    {
        _context.Datasets.Remove(dataset);
        await _context.SaveChangesAsync(ct);
    }
}

