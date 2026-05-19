namespace ChartEngine.Infrastructure.BackgroundJobs;

using System.Threading.Channels;

/// <summary>
/// Thread-safe in-memory queue for dataset processing jobs.
/// Registered as Singleton â€” one channel shared across the entire app lifetime.
/// Producer: DatasetService (enqueues datasetId after successful upload)
/// Consumer: DatasetProcessingWorker (picks up jobs in background loop)
/// </summary>
public sealed class DatasetProcessingChannel
{
    
    
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        });

    /// <summary>
    /// Called by DatasetService after a successful file upload.
    /// Returns false if the channel is closed â€” log it and continue.
    /// </summary>
    public bool TryEnqueue(Guid datasetId)
        => _channel.Writer.TryWrite(datasetId);

    /// <summary>
    /// Called by DatasetProcessingWorker in an infinite loop.
    /// Suspends when queue is empty, resumes when item arrives.
    /// Does NOT busy-wait â€” very efficient.
    /// </summary>
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}

