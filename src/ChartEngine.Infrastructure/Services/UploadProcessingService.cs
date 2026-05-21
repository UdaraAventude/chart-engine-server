using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;
using ChartEngine.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace ChartEngine.Infrastructure.Services;

public class UploadProcessingService : IUploadProcessingService
{
    private readonly IFileStorage _storage;
    private readonly IDatasetRepository _datasetRepository;
    private readonly ITreeRepository _treeRepository;
    private readonly ISchemaDetector _schemaDetector;
    private readonly ITreeBuilder _treeBuilder;

    public UploadProcessingService(
        IFileStorage storage,
        IDatasetRepository datasetRepository,
        ITreeRepository treeRepository,
        ISchemaDetector schemaDetector,
        ITreeBuilder treeBuilder)
    {
        _storage = storage;
        _datasetRepository = datasetRepository;
        _treeRepository = treeRepository;
        _schemaDetector = schemaDetector;
        _treeBuilder = treeBuilder;
    }

    public async Task<UploadCompletedResult> ProcessAsync(IFormFile file, Func<int, Task> onProgressAsync)
    {
        await onProgressAsync(5); // Started

        // 1. Initial save & record
        var dataset = new Dataset(file.FileName, file.Length);
        await _datasetRepository.AddAsync(dataset);
        var path = await _storage.SaveFileAsync(file.OpenReadStream(), dataset.Id.ToString(), file.FileName);
        
        dataset.UpdateStatus(DatasetStatus.Processing);
        await _datasetRepository.UpdateAsync(dataset);
        await onProgressAsync(10); // File saved

        // 2. Schema Detection
        var schema = await _schemaDetector.DetectSchemaAsync(path);
        
        var dimensions = schema.Columns.Count(c => c.Type == ColumnType.Categorical);
        var metrics = schema.Columns.Count(c => c.Type == ColumnType.Numerical);
        await onProgressAsync(30); // Schema detected

        // 3. Tree Building
        var treeNode = await _treeBuilder.BuildTreeAsync(path, schema);
        var jsonTree = JsonSerializer.Serialize(treeNode);

        var treeEntity = new Tree(dataset.Id, jsonTree);
        await _treeRepository.AddAsync(treeEntity);
        await onProgressAsync(90); // Tree built

        // 4. Finalize
        dataset.SetTotalRows(treeNode.Count);
        dataset.UpdateStatus(DatasetStatus.Ready);
        await _datasetRepository.UpdateAsync(dataset);
        await onProgressAsync(100); // Done

        return new UploadCompletedResult(dataset.Id, treeNode.Count, dimensions, metrics);
    }
}
