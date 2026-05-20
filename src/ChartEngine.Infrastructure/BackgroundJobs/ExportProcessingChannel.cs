using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace ChartEngine.Infrastructure.BackgroundJobs;

public sealed class ExportProcessingChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        });

    public bool TryEnqueue(Guid jobId)
        => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
