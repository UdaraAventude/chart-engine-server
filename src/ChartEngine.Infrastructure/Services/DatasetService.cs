namespace ChartEngine.Infrastructure.Services;

using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Exceptions;
using ChartEngine.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class DatasetService : IDatasetService
{
    private readonly IDatasetRepository _repository;
    private readonly IFileStorage _fileStorage;
    private readonly DatasetProcessingChannel _channel;
    private readonly ILogger<DatasetService> _logger;

    public DatasetService(
        IDatasetRepository repository,
        IFileStorage fileStorage,
        DatasetProcessingChannel channel,
        ILogger<DatasetService> logger)
    {
        _repository = repository;
        _fileStorage = fileStorage;
        _channel = channel;
        _logger = logger;
    }

    public async Task<DatasetUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        // 1. Create the domain entity FIRST — this validates the input
        //    If the file name is empty, Dataset.Create() throws before we touch disk
        var dataset = Dataset.Create(
            fileName: file.FileName,
            fileSizeBytes: file.Length,
            storagePath: string.Empty  // we don't know the path yet
        );

        // 2. Save the file to disk using the dataset's ID as the filename
        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorage.SaveAsync(dataset.Id, stream, file.FileName, ct);

        // 3. Now we know the path — set it on the entity
        dataset.SetStoragePath(storagePath);

        // 4. Persist to database
        await _repository.AddAsync(dataset, ct);

        // 5. Enqueue for background processing
        //    The background worker will pick this up and process the CSV
        var enqueued = _channel.TryEnqueue(dataset.Id);
        if (!enqueued)
            _logger.LogWarning("Failed to enqueue dataset {DatasetId} for processing", dataset.Id);

        // 6. Return a DTO — not the raw entity
        return new DatasetUploadResult(
            DatasetId: dataset.Id,
            Status: dataset.Status.ToString(),
            FileName: dataset.FileName
        );
    }

    public async Task<DatasetStatusDto> GetStatusAsync(Guid datasetId, CancellationToken ct = default)
    {
        var dataset = await _repository.GetByIdAsync(datasetId, ct);

        // Throw our custom domain exception — the middleware will turn this into HTTP 404
        if (dataset is null)
            throw new DatasetNotFoundException(datasetId);

        return new DatasetStatusDto(
            DatasetId: dataset.Id,
            FileName: dataset.FileName,
            Status: dataset.Status.ToString(),
            TotalRows: dataset.TotalRows,
            CreatedAt: dataset.CreatedAt,
            ProcessedAt: dataset.ProcessedAt,
            ErrorMessage: dataset.ErrorMessage
        );
    }
}
