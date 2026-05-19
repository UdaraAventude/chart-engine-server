namespace ChartEngine.Infrastructure.BackgroundJobs;

using ChartEngine.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Long-running background service that processes datasets.
/// Runs for the entire lifetime of the application.
/// Picks up datasetIds from the channel one-by-one and processes them.
/// </summary>
public sealed class DatasetProcessingWorker : BackgroundService
{
    private readonly DatasetProcessingChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatasetProcessingWorker> _logger;

    public DatasetProcessingWorker(
        DatasetProcessingChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<DatasetProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Main loop — runs until stoppingToken is cancelled (app shutdown).
    /// ReadAllAsync suspends here when queue is empty.
    /// Resumes each time a datasetId is enqueued.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dataset processing worker started");

        try
        {
            await foreach (var datasetId in _channel.ReadAllAsync(stoppingToken))
            {
                await ProcessOneAsync(datasetId, stoppingToken);
            }
        }
        finally
        {
            _logger.LogInformation("Dataset processing worker stopped");
        }
    }

    /// <summary>
    /// Process a single dataset.
    /// Creates a fresh DI scope so scoped services (DbContext, pipeline) are fresh per job.
    /// If the job fails, the dataset is marked as Failed and we continue to the next job.
    /// </summary>
    private async Task ProcessOneAsync(Guid datasetId, CancellationToken ct)
    {
        _logger.LogInformation("Worker picked up dataset {DatasetId}", datasetId);

        // Create a fresh DI scope for this job.
        // When the using block ends, the scope is disposed —
        // the DbContext connection is returned to the pool, memory is freed.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IDatasetPipelineService>();

        try
        {
            await pipeline.ProcessAsync(datasetId, ct);
        }
        catch (OperationCanceledException)
        {
            // App is shutting down mid-job — fine, will be re-queued on restart
            _logger.LogWarning("Processing of dataset {DatasetId} was cancelled", datasetId);
        }
        catch (Exception ex)
        {
            // Catch here so one failed job doesn't kill the worker loop.
            // The pipeline already marked the dataset as Failed in the DB.
            _logger.LogError(ex, "Worker caught unhandled error for dataset {DatasetId}", datasetId);
        }
    }
}
