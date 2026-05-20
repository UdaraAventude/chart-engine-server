namespace ChartEngine.Infrastructure.Services;

using ChartEngine.Application.Hubs;
using ChartEngine.Application.Interfaces.Analytics;
using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

public class DatasetPipelineService : IDatasetPipelineService
{
    private readonly IDatasetRepository _datasetRepository;
    private readonly ITreeRepository _treeRepository;
    private readonly ISchemaDetector _schemaDetector;
    private readonly ITreeBuilder _treeBuilder;
    private readonly IFileStorage _fileStorage;
    private readonly IHubContext<DatasetHub> _hub;
    private readonly ILogger<DatasetPipelineService> _logger;
    private readonly ChartEngine.Infrastructure.Persistence.AppDbContext _dbContext;

    
    
    private const int SchemaSampleSize = 500;

    public DatasetPipelineService(
        IDatasetRepository datasetRepository,
        ITreeRepository treeRepository,
        ISchemaDetector schemaDetector,
        ITreeBuilder treeBuilder,
        IFileStorage fileStorage,
        IHubContext<DatasetHub> hub,
        ILogger<DatasetPipelineService> logger,
        ChartEngine.Infrastructure.Persistence.AppDbContext dbContext)
    {
        _datasetRepository = datasetRepository;
        _treeRepository = treeRepository;
        _schemaDetector = schemaDetector;
        _treeBuilder = treeBuilder;
        _fileStorage = fileStorage;
        _hub = hub;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task ProcessAsync(Guid datasetId, CancellationToken ct = default)
    {
        _logger.LogInformation("Pipeline starting for dataset {DatasetId}", datasetId);

        var dataset = await _datasetRepository.GetByIdAsync(datasetId, ct)
            ?? throw new DatasetNotFoundException(datasetId);

        dataset.MarkAsProcessing();
        await _datasetRepository.UpdateAsync(dataset, ct);
        await PushProgressAsync(datasetId, 5, "Starting pipeline", ct);

        try
        {
            
            await PushProgressAsync(datasetId, 10, "Reading sample rows", ct);
            var sampleRows = await ReadSampleRowsAsync(dataset.StoragePath, SchemaSampleSize);

            
            int estimatedRowCount = (int)(dataset.FileSizeBytes / 100);

            
            await PushProgressAsync(datasetId, 20, "Detecting schema", ct);
            var schema = _schemaDetector.Detect(sampleRows, estimatedRowCount);

            _logger.LogInformation(
                "Dataset {DatasetId}: {DimCount} dimensions, {MetricCount} metrics, {RejectCount} rejected",
                datasetId, schema.Dimensions.Count, schema.Metrics.Count, schema.Rejected.Count);

            
            await PersistSchemaAsync(datasetId, schema, ct);
            await PushProgressAsync(datasetId, 30, "Schema detected", ct);

            
            await PushProgressAsync(datasetId, 35, "Building aggregation tree", ct);

            var (root, totalRows) = await _treeBuilder.BuildAsync(
                storagePath: dataset.StoragePath,
                schema: schema,
                onProgress: async percent =>
                {
                    
                    int mapped = 35 + (int)(percent * 0.55);
                    await PushProgressAsync(datasetId, mapped, "Building tree", ct);
                },
                ct: ct);

            _logger.LogInformation(
                "Dataset {DatasetId}: tree built, {TotalRows} rows processed",
                datasetId, totalRows);

            
            await PushProgressAsync(datasetId, 92, "Saving tree", ct);
            await _treeRepository.SaveAsync(datasetId, root, schema, totalRows, ct);

            
            dataset.MarkAsReady(totalRows);
            await _datasetRepository.UpdateAsync(dataset, ct);

            await PushProgressAsync(datasetId, 100, "Complete", ct);

            
            await _hub.Clients
                .Group(ChartEngine.Application.Constants.DatasetHubGroups.GetGroupName(datasetId))
                .SendAsync("DatasetReady", new
                {
                    datasetId,
                    totalRows,
                    dimensions = schema.Dimensions,
                    metrics = schema.Metrics
                }, ct);

            _logger.LogInformation("Dataset {DatasetId} is Ready", datasetId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Pipeline failed for dataset {DatasetId}", datasetId);
            dataset.MarkAsFailed(ex.Message);
            await _datasetRepository.UpdateAsync(dataset, ct);

            await _hub.Clients
                .Group(ChartEngine.Application.Constants.DatasetHubGroups.GetGroupName(datasetId))
                .SendAsync("DatasetFailed", new
                {
                    datasetId,
                    error = ex.Message
                }, ct);

            throw;
        }
    }

    
    
    private static async Task<List<Dictionary<string, string>>> ReadSampleRowsAsync(
        string storagePath, int maxRows)
    {
        var rows = new List<Dictionary<string, string>>(maxRows);

        using var reader = new StreamReader(storagePath);
        using var csv = new CsvHelper.CsvReader(reader,
            new CsvHelper.Configuration.CsvConfiguration(
                System.Globalization.CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            });

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync() && rows.Count < maxRows)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in csv.HeaderRecord!)
                row[header] = csv.GetField(header) ?? string.Empty;
            rows.Add(row);
        }

        return rows;
    }

    
    private async Task PersistSchemaAsync(
        Guid datasetId,
        ChartEngine.Domain.ValueObjects.DatasetSchema schema,
        CancellationToken ct)
    {
        int order = 0;

        var columns = new List<ChartEngine.Domain.Entities.DatasetColumn>();

        // Dimensions
        foreach (var dim in schema.Dimensions)
            columns.Add(ChartEngine.Domain.Entities.DatasetColumn.AsDimension(datasetId, dim, cardinality: 0, order: order++));

        // Metrics
        foreach (var metric in schema.Metrics)
            columns.Add(ChartEngine.Domain.Entities.DatasetColumn.AsMetric(datasetId, metric, order: order++));

        // Rejected columns
        foreach (var rejected in schema.Rejected)
            columns.Add(ChartEngine.Domain.Entities.DatasetColumn.AsRejected(datasetId, rejected.Name, rejected.RejectionReason ?? "unknown", rejected.Cardinality, order: order++));

        await _datasetRepository.SaveColumnsAsync(columns, ct);

        // Persist the dynamic config (cardinality limits, depth, etc.)
        var config = ChartEngine.Domain.Entities.DatasetConfig.Create(
            datasetId,
            schema.Config.MaxDimCardinality,
            schema.Config.MaxHierarchyDepth,
            schema.Config.FallbackTopValues);

        await _datasetRepository.SaveConfigAsync(config, ct);

        _logger.LogInformation(
            "Schema persisted for dataset {DatasetId}: {Dims} dims, {Metrics} metrics, {Rejected} rejected",
            datasetId, schema.Dimensions.Count, schema.Metrics.Count, schema.Rejected.Count);
    }

    private async Task PushProgressAsync(Guid datasetId, int percent, string message, CancellationToken ct)
    {
        _logger.LogDebug("Dataset {DatasetId}: {Percent}% â€” {Message}", datasetId, percent, message);

        await _hub.Clients
            .Group(ChartEngine.Application.Constants.DatasetHubGroups.GetGroupName(datasetId))
            .SendAsync("DatasetProgress", new { datasetId, percent, message }, ct);
    }
}
