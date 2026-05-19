namespace ChartEngine.Application.Interfaces.Analytics;

using ChartEngine.Domain.ValueObjects;

public interface ITreeBuilder
{
    
    
    
    
    Task<(TreeNode Root, int TotalRows)> BuildAsync(
        string storagePath,
        DatasetSchema schema,
        Func<int, Task> onProgress,
        CancellationToken ct = default);
}

