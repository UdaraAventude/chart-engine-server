namespace ChartEngine.Infrastructure.Services;

using ChartEngine.Application.Constants;
using ChartEngine.Application.Hubs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

public class DatasetPipelineService : IDatasetPipelineService
{
    private readonly IDatasetRepository _repository;
    private readonly IHubContext<DatasetHub> _hub;
    private readonly ILogger<DatasetPipelineService> _logger;

    public DatasetPipelineService(
        IDatasetRepository repository,
        IHubContext<DatasetHub> hub,
        ILogger<DatasetPipelineService> logger)
    {
        _repository = repository;
        _hub = hub;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid datasetId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting processing for dataset {DatasetId}", datasetId);

        var dataset = await _repository.GetByIdAsync(datasetId, ct)
            ?? throw new DatasetNotFoundException(datasetId);

        // Transition: Pending → Processing
        dataset.MarkAsProcessing();
        await _repository.UpdateAsync(dataset, ct);

        // Push 0% progress — frontend shows "Starting..."
        await PushProgressAsync(datasetId, 0, "Starting pipeline", ct);

        try
        {
            // STUB: simulate work for now — real CSV logic replaces this
            _logger.LogInformation("Processing dataset {DatasetId} (stub)", datasetId);

            await Task.Delay(1000, ct);
            await PushProgressAsync(datasetId, 25, "Detecting schema", ct);

            await Task.Delay(1000, ct);
            await PushProgressAsync(datasetId, 75, "Building aggregation tree", ct);

            await Task.Delay(1000, ct);
            await PushProgressAsync(datasetId, 95, "Saving results", ct);

            // Transition: Processing → Ready
            dataset.MarkAsReady(totalRows: 0);   // 0 for now, real count comes later
            await _repository.UpdateAsync(dataset, ct);

            // Push the Ready event — frontend reacts by displaying results
            await _hub.Clients
                .Group(DatasetHubGroups.GetGroupName(datasetId))
                .SendAsync("DatasetReady", new
                {
                    datasetId,
                    totalRows = dataset.TotalRows
                }, cancellationToken: ct);

            _logger.LogInformation("Dataset {DatasetId} Ready", datasetId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to process dataset {DatasetId}", datasetId);

            // Transition: Processing → Failed
            dataset.MarkAsFailed(ex.Message);
            await _repository.UpdateAsync(dataset, ct);

            // Push the Failed event — frontend shows error
            await _hub.Clients
                .Group(DatasetHubGroups.GetGroupName(datasetId))
                .SendAsync("DatasetFailed", new
                {
                    datasetId,
                    error = ex.Message
                }, cancellationToken: ct);

            throw;
        }
    }

    private async Task PushProgressAsync(
        Guid datasetId, int percent, string message, CancellationToken ct)
    {
        _logger.LogInformation("Dataset {DatasetId}: {Percent}% — {Message}",
            datasetId, percent, message);

        await _hub.Clients
            .Group(DatasetHubGroups.GetGroupName(datasetId))
            .SendAsync("DatasetProgress", new
            {
                datasetId,
                percent,
                message
            }, cancellationToken: ct);
    }
}


