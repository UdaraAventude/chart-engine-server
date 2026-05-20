using ChartEngine.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.Application.Interfaces.Repositories;

public interface IExportRepository
{
    Task<ExportJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ExportJob job, CancellationToken ct = default);
    Task UpdateAsync(ExportJob job, CancellationToken ct = default);
}
