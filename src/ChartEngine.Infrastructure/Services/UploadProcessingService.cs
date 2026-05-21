using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.ValueObjects;
using ChartEngine.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

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
        var dataset = Dataset.Create(file.FileName, file.Length, "temp");
        await _datasetRepository.AddAsync(dataset);
        var path = await _storage.SaveAsync(dataset.Id, file.OpenReadStream(), file.FileName);
        
        dataset.SetStoragePath(path);
        dataset.MarkAsProcessing();
        await _datasetRepository.UpdateAsync(dataset);
        await onProgressAsync(10); // File saved

        // 2. Schema Detection
        var sampleRows = new List<Dictionary<string, string>>();
        using (var reader = new StreamReader(path))
        using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true }))
        {
            await csv.ReadAsync();
            csv.ReadHeader();
            var headers = csv.HeaderRecord;
            if (headers != null)
            {
                int count = 0;
                while (await csv.ReadAsync() && count < 500)
                {
                    var row = new Dictionary<string, string>();
                    for (int i = 0; i < headers.Length; i++)
                    {
                        row[headers[i]] = csv.GetField(i) ?? string.Empty;
                    }
                    sampleRows.Add(row);
                    count++;
                }
            }
        }
        
        int estimatedRowCount = (int)(file.Length / 100);
        var schema = _schemaDetector.Detect(sampleRows, estimatedRowCount);
        
        var dimensions = schema.Dimensions.Count;
        var metrics = schema.Metrics.Count;
        await onProgressAsync(30); // Schema detected

        // 3. Tree Building
        var treeResult = await _treeBuilder.BuildAsync(path, schema, async (p) => {
            // map 0-100% of tree builder to 30-90% overall
            await onProgressAsync(30 + (int)(p * 0.6));
        });
        
        var treeNode = treeResult.Root;
        await onProgressAsync(92);
        await _treeRepository.SaveAsync(dataset.Id, treeNode, schema, treeResult.TotalRows);

        // 4. Finalize
        dataset.MarkAsReady(treeResult.TotalRows);
        await _datasetRepository.UpdateAsync(dataset);
        await onProgressAsync(100); // Done

        return new UploadCompletedResult(
            dataset.Id,
            treeResult.TotalRows,
            dimensions,
            metrics,
            schema.Dimensions,
            schema.Metrics);
    }
}
