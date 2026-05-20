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
using System.Text.Json;

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

        // Guardrail: Protect server memory and network bandwidth from massive payloads
        const int MaxRowsForFullTree = 100_000;
        if (dataset.TotalRows > MaxRowsForFullTree)
        {
            throw new InvalidOperationException(
                $"Dataset is too large ({dataset.TotalRows:N0} rows) to retrieve as a full tree. " +
                $"Please use the `/drill` endpoint to lazy-load branches on-demand.");
        }

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

    public async Task<object?> GetDrillDownAsync(Guid datasetId, string[]? path, CancellationToken ct = default)
    {
        var dataset = await _repository.GetByIdAsync(datasetId, ct);
        if (dataset is null)
            throw new DatasetNotFoundException(datasetId);

        if (dataset.Status != DatasetStatus.Ready)
            throw new InvalidOperationException($"Dataset {datasetId} is not ready yet.");

        var treeJson = await _treeRepository.GetTreeJsonAsync(datasetId, ct);
        if (string.IsNullOrEmpty(treeJson))
            return null;

        // Parse using memory-efficient JsonDocument
        using var doc = JsonDocument.Parse(treeJson);
        var root = doc.RootElement.GetProperty("tree");

        var currentNode = root;
        path ??= Array.Empty<string>();

        foreach (var segment in path)
        {
            if (string.IsNullOrWhiteSpace(segment)) continue;

            var found = false;
            if (currentNode.TryGetProperty("children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in childrenElement.EnumerateArray())
                {
                    if (child.TryGetProperty("name", out var nameElement) &&
                        string.Equals(nameElement.GetString(), segment, StringComparison.OrdinalIgnoreCase))
                    {
                        currentNode = child;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // Path not found
                return null;
            }
        }

        // Project the target node and its immediate children (without their deep sub-children)
        var childrenList = new List<object>();
        if (currentNode.TryGetProperty("children", out var targetChildren) && targetChildren.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in targetChildren.EnumerateArray())
            {
                childrenList.Add(new
                {
                    name = child.GetProperty("name").GetString(),
                    count = child.GetProperty("count").GetInt32(),
                    value = child.GetProperty("value").GetDouble(),
                    aggs = child.GetProperty("aggs").Clone()
                });
            }
        }

        // Return a clean, dynamic object containing node details & top-level children
        return new
        {
            name = currentNode.GetProperty("name").GetString(),
            count = currentNode.GetProperty("count").GetInt32(),
            value = currentNode.GetProperty("value").GetDouble(),
            aggs = currentNode.GetProperty("aggs").Clone(),
            children = childrenList
        };
    }
}

