namespace ChartEngine.Tests;

using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Domain.Entities;
using ChartEngine.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class RowQueryServiceTests
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

        public Task<PagedListDto<DatasetListDto>> GetPagedDatasetsAsync(int page, int pageSize, string? sortBy, string? search, CancellationToken ct = default)
        {
            return Task.FromResult(new PagedListDto<DatasetListDto>(new(), 0, page, pageSize));
        }

        public Task DeleteAsync(Dataset dataset, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetPagedRowsAsync_returns_unfiltered_rows_when_drillPath_is_null()
    {
        var csvPath = WriteTempCsv("""
            Name,Age,Country
            Alice,25,Netherlands
            Bob,30,USA
            Charlie,35,Netherlands
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);
        var repo = new FakeDatasetRepository(dataset);
        var service = new RowQueryService(repo, new FakeLogger<RowQueryService>());

        var result = await service.GetPagedRowsAsync(dataset.Id, page: 1, pageSize: 2, drillPathJson: null);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Alice", result.Rows[0]["Name"]);
        Assert.Equal("Bob", result.Rows[1]["Name"]);
    }

    [Fact]
    public async Task GetPagedRowsAsync_filters_by_standard_dimension()
    {
        var csvPath = WriteTempCsv("""
            Name,Age,Country
            Alice,25,Netherlands
            Bob,30,USA
            Charlie,35,Netherlands
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);
        var repo = new FakeDatasetRepository(dataset);
        var service = new RowQueryService(repo, new FakeLogger<RowQueryService>());

        var drillPath = """[{"column": "Country", "value": "Netherlands"}]""";
        var result = await service.GetPagedRowsAsync(dataset.Id, page: 1, pageSize: 10, drillPathJson: drillPath);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal("Netherlands", r["Country"]));
        Assert.Equal("Alice", result.Rows[0]["Name"]);
        Assert.Equal("Charlie", result.Rows[1]["Name"]);
    }

    [Fact]
    public async Task GetPagedRowsAsync_filters_by_histogram_range_inclusive()
    {
        var csvPath = WriteTempCsv("""
            Name,Age
            Alice,15
            Bob,20
            Charlie,25
            Dave,30
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);
        var repo = new FakeDatasetRepository(dataset);
        var service = new RowQueryService(repo, new FakeLogger<RowQueryService>());

        // Test inclusive range e.g. [20-30]
        var drillPath = """[{"column": "__hist__Age", "value": "[20-30]"}]""";
        var result = await service.GetPagedRowsAsync(dataset.Id, page: 1, pageSize: 10, drillPathJson: drillPath);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Rows, r => r["Name"] == "Bob");
        Assert.Contains(result.Rows, r => r["Name"] == "Charlie");
        Assert.Contains(result.Rows, r => r["Name"] == "Dave");
    }

    [Fact]
    public async Task GetPagedRowsAsync_filters_by_histogram_range_exclusive()
    {
        var csvPath = WriteTempCsv("""
            Name,Age
            Alice,15
            Bob,20
            Charlie,25
            Dave,30
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);
        var repo = new FakeDatasetRepository(dataset);
        var service = new RowQueryService(repo, new FakeLogger<RowQueryService>());

        // Test exclusive min, inclusive max e.g. (20-30]
        var drillPath = """[{"column": "__hist__Age", "value": "(20-30]"}]""";
        var result = await service.GetPagedRowsAsync(dataset.Id, page: 1, pageSize: 10, drillPathJson: drillPath);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, r => r["Name"] == "Charlie");
        Assert.Contains(result.Rows, r => r["Name"] == "Dave");
        Assert.DoesNotContain(result.Rows, r => r["Name"] == "Bob");
    }

    [Fact]
    public async Task GetPagedRowsAsync_filters_by_histogram_range_with_different_separators()
    {
        var csvPath = WriteTempCsv("""
            Name,Age
            Alice,10
            Bob,15
            Charlie,20
            """);

        var dataset = Dataset.Create("test.csv", 100, csvPath);
        var repo = new FakeDatasetRepository(dataset);
        var service = new RowQueryService(repo, new FakeLogger<RowQueryService>());

        // Test underscore separator, e.g. 10_20 (which evaluates as [10-20] inclusive)
        var drillPath = """[{"column": "__hist__Age", "value": "10_20"}]""";
        var result = await service.GetPagedRowsAsync(dataset.Id, page: 1, pageSize: 10, drillPathJson: drillPath);

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Rows.Count);
    }
}
