using ChartEngine.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.Infrastructure.BackgroundJobs;

public sealed class ExportProcessingWorker : BackgroundService
{
    private readonly ExportProcessingChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExportProcessingWorker> _logger;

    public ExportProcessingWorker(
        ExportProcessingChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ExportProcessingWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Export processing worker started");

        try
        {
            await foreach (var jobId in _channel.ReadAllAsync(stoppingToken))
            {
                await ProcessOneAsync(jobId, stoppingToken);
            }
        }
        finally
        {
            _logger.LogInformation("Export processing worker stopped");
        }
    }

    private async Task ProcessOneAsync(Guid jobId, CancellationToken ct)
    {
        _logger.LogInformation("Worker picked up export job {JobId}", jobId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var exportService = scope.ServiceProvider.GetRequiredService<IExportService>();

        try
        {
            await exportService.ProcessExportAsync(jobId, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processing of export job {JobId} was cancelled", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker caught unhandled error for export job {JobId}", jobId);
        }
    }
}
