namespace ChartEngine.Application.Interfaces.Analytics;

using ChartEngine.Domain.ValueObjects;

public interface ITreeBuilder
{
    // Builds the full aggregation tree by streaming rows from the CSV.
    // storagePath: path to the saved CSV file on disk
    // schema: the detected schema (which columns are dimensions, which are metrics)
    // progress: callback to report progress percentage (0-100) during building
    Task<(TreeNode Root, int TotalRows)> BuildAsync(
        string storagePath,
        DatasetSchema schema,
        Func<int, Task> onProgress,
        CancellationToken ct = default);
}
