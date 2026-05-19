namespace ChartEngine.Application.Interfaces.Repositories;

using ChartEngine.Domain.ValueObjects;

public interface ITreeRepository
{
    Task SaveAsync(Guid datasetId, TreeNode root, DatasetSchema schema, int totalRows, CancellationToken ct = default);
    Task<string?> GetTreeJsonAsync(Guid datasetId, CancellationToken ct = default);
}
