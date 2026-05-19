namespace ChartEngine.Application.Interfaces.Services;

public interface IDatasetPipelineService
{
    /// <summary>
    /// Process a dataset: detect schema, build tree, save to DB.
    /// Transitions state: Pending → Processing → Ready/Failed.
    /// Pushes SignalR events for progress and completion.
    /// </summary>
    Task ProcessAsync(Guid datasetId, CancellationToken ct = default);
}
