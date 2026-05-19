namespace ChartEngine.Infrastructure.Services;

using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Enums;
using ChartEngine.Domain.Exceptions;
using ChartEngine.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class DatasetService : IDatasetService
{
    private readonly IDatasetRepository _repository;
    private readonly ITreeRepository _treeRepository;
    private readonly IFileStorage _fileStorage;
    private readonly DatasetProcessingChannel _channel;
    private readonly ILogger<DatasetService> _logger;

    public DatasetService(
        IDatasetRepository repository,
        ITreeRepository treeRepository,
        IFileStorage fileStorage,
        DatasetProcessingChannel channel,
        ILogger<DatasetService> logger)
    {
        _repository = repository;
        _treeRepository = treeRepository;
        _fileStorage = fileStorage;
        _channel = channel;
        _logger = logger;
    }

    public async Task<DatasetUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        
        
        var dataset = Dataset.Create(
            fileName: file.FileName,
            fileSizeBytes: file.Length,
            storagePath: string.Empty  
        );

        
        await using var stream = file.OpenReadStream();
        var storagePath = await _fileStorage.SaveAsync(dataset.Id, stream, file.FileName, ct);

        
        dataset.SetStoragePath(storagePath);

        
        await _repository.AddAsync(dataset, ct);

        
        
        var enqueued = _channel.TryEnqueue(dataset.Id);
        if (!enqueued)
            _logger.LogWarning("Failed to enqueue dataset {DatasetId} for processing", dataset.Id);

        
        return new DatasetUploadResult(
            DatasetId: dataset.Id,
            Status: dataset.Status.ToString(),
            FileName: dataset.FileName
        );
    }

    public async Task<DatasetStatusDto> GetStatusAsync(Guid datasetId, CancellationToken ct = default)
    {
        var dataset = await _repository.GetByIdAsync(datasetId, ct);

        
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

    public async Task<string?> GetTreeJsonAsync(Guid datasetId, CancellationToken ct = default)
    {
        var dataset = await _repository.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            throw new DatasetNotFoundException(datasetId);

        if (dataset.Status != DatasetStatus.Ready)
            throw new InvalidOperationException($"Dataset {datasetId} is not ready yet.");

        return await _treeRepository.GetTreeJsonAsync(datasetId, ct);
    }

    public async Task<DatasetSchemaDto> GetSchemaAsync(Guid datasetId, CancellationToken ct = default)
    {
        var columns = await _repository.GetColumnsAsync(datasetId, ct);
        
        if (!columns.Any())
            throw new DatasetNotFoundException(datasetId);

        var dimensions = columns
            .Where(c => c.Role == ColumnRole.Dimension)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.ColumnName)
            .ToList();

        var metrics = columns
            .Where(c => c.Role == ColumnRole.Metric)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.ColumnName)
            .ToList();

        return new DatasetSchemaDto(datasetId, dimensions, metrics);
    }
}

