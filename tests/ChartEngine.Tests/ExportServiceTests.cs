namespace ChartEngine.Tests;

using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Exceptions;
using ChartEngine.Infrastructure.BackgroundJobs;
using ChartEngine.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class ExportServiceTests
{
    private static string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    private class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class FakeDatasetRepository : IDatasetRepository
    {
        private readonly Dataset _dataset;

        public FakeDatasetRepository(Dataset dataset)
        {
            _dataset = dataset;
        }

        public Task<Dataset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult<Dataset?>(_dataset.Id == id ? _dataset : null);
        }

        public Task AddAsync(Dataset dataset, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Dataset dataset, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveColumnsAsync(IEnumerable<DatasetColumn> columns, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IEnumerable<DatasetColumn>> GetColumnsAsync(Guid datasetId, CancellationToken ct = default) => Task.FromResult<IEnumerable<DatasetColumn>>(Array.Empty<DatasetColumn>());
        public Task SaveConfigAsync(DatasetConfig config, CancellationToken ct = default) => Task.CompletedTask;
        public Task<DatasetConfig?> GetConfigAsync(Guid datasetId, CancellationToken ct = default) =>
            Task.FromResult<DatasetConfig?>(null);

        public Task<PagedListDto<DatasetListDto>> GetPagedDatasetsAsync(int page, int pageSize, string? sortBy, string? search, CancellationToken ct = default)
        {
            return Task.FromResult(new PagedListDto<DatasetListDto>(new(), 0, page, pageSize));
        }

        public Task DeleteAsync(Dataset dataset, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeExportRepository : IExportRepository
    {
        public readonly Dictionary<Guid, ExportJob> Jobs = new();

        public Task<ExportJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Jobs.TryGetValue(id, out var job) ? job : null);
        }

        public Task AddAsync(ExportJob job, CancellationToken ct = default)
        {
            Jobs[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ExportJob job, CancellationToken ct = default)
        {
            Jobs[job.Id] = job;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StartExportAsync_creates_pending_job_and_enqueues()
    {
        var csvPath = WriteTempCsv("Name,Age\nAlice,25\n");
        var dataset = Dataset.Create("test.csv", 100, csvPath);

        var datasetRepo = new FakeDatasetRepository(dataset);
        var exportRepo = new FakeExportRepository();
        var channel = new ExportProcessingChannel();
        
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Storage:BasePath", Path.GetTempPath() }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new ExportService(datasetRepo, exportRepo, channel, config, new FakeLogger<ExportService>());

        var result = await service.StartExportAsync(dataset.Id, "CSV", null);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("CSV", result.Format);
        Assert.Equal(dataset.Id, result.DatasetId);

        // Check it is saved in the repository
        Assert.Contains(result.JobId, exportRepo.Jobs.Keys);
        
        // Check it is enqueued in the channel
        var cts = new CancellationTokenSource(100);
        var enqueuedJobs = new List<Guid>();
        await foreach (var jobId in channel.ReadAllAsync(cts.Token))
        {
            enqueuedJobs.Add(jobId);
            break;
        }
        Assert.Contains(result.JobId, enqueuedJobs);
    }

    [Fact]
    public async Task ProcessExportAsync_filters_and_writes_csv()
    {
        var csvPath = WriteTempCsv("""
            Name,Age,Country
            Alice,25,Netherlands
            Bob,30,USA
            Charlie,35,Netherlands
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);

        var datasetRepo = new FakeDatasetRepository(dataset);
        var exportRepo = new FakeExportRepository();
        var channel = new ExportProcessingChannel();
        
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Storage:BasePath", baseDir }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new ExportService(datasetRepo, exportRepo, channel, config, new FakeLogger<ExportService>());

        var drillPath = """[{"column": "Country", "value": "Netherlands"}]""";
        var result = await service.StartExportAsync(dataset.Id, "CSV", drillPath);

        // Execute background processing synchronously
        await service.ProcessExportAsync(result.JobId);

        // Fetch job status
        var status = await service.GetStatusAsync(result.JobId);
        Assert.NotNull(status);
        Assert.Equal("Completed", status.Status);
        Assert.NotNull(status.DownloadUrl);

        // Verify exported file contents
        var job = exportRepo.Jobs[result.JobId];
        Assert.NotNull(job.DownloadPath);
        Assert.True(File.Exists(job.DownloadPath));

        var exportedLines = File.ReadAllLines(job.DownloadPath);
        Assert.Equal(3, exportedLines.Length); // Header + 2 data rows
        Assert.Equal("Name,Age,Country", exportedLines[0]);
        Assert.Equal("Alice,25,Netherlands", exportedLines[1]);
        Assert.Equal("Charlie,35,Netherlands", exportedLines[2]);

        // Cleanup
        Directory.Delete(baseDir, true);
    }

    [Fact]
    public async Task ProcessExportAsync_filters_and_writes_excel()
    {
        var csvPath = WriteTempCsv("""
            Name,Age,Country
            Alice,25,Netherlands
            Bob,30,USA
            Charlie,35,Netherlands
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);

        var datasetRepo = new FakeDatasetRepository(dataset);
        var exportRepo = new FakeExportRepository();
        var channel = new ExportProcessingChannel();
        
        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Storage:BasePath", baseDir }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new ExportService(datasetRepo, exportRepo, channel, config, new FakeLogger<ExportService>());

        // Try high-performance filtering based on numeric age range e.g. [30-40]
        var drillPath = """[{"column": "__hist__Age", "value": "[30-40]"}]""";
        var result = await service.StartExportAsync(dataset.Id, "Excel", drillPath);

        // Execute background processing
        await service.ProcessExportAsync(result.JobId);

        // Fetch job status
        var status = await service.GetStatusAsync(result.JobId);
        Assert.NotNull(status);
        Assert.Equal("Completed", status.Status);

        // Verify exported file exists
        var job = exportRepo.Jobs[result.JobId];
        Assert.NotNull(job.DownloadPath);
        Assert.True(File.Exists(job.DownloadPath));
        Assert.EndsWith(".xlsx", job.DownloadPath);

        // Cleanup
        Directory.Delete(baseDir, true);
    }

    [Fact]
    public async Task DownloadExportAsync_throws_when_not_ready()
    {
        var csvPath = WriteTempCsv("Name,Age\nAlice,25\n");
        var dataset = Dataset.Create("test.csv", 100, csvPath);

        var datasetRepo = new FakeDatasetRepository(dataset);
        var exportRepo = new FakeExportRepository();
        var channel = new ExportProcessingChannel();

        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Storage:BasePath", Path.GetTempPath() }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();

        var service = new ExportService(datasetRepo, exportRepo, channel, config, new FakeLogger<ExportService>());

        var result = await service.StartExportAsync(dataset.Id, "CSV", null);

        // Don't call ProcessExportAsync (keeps status as Pending)
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadExportAsync(result.JobId));
    }
}
