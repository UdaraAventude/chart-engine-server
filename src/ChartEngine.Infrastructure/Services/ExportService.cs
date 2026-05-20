using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Entities;
using ChartEngine.Domain.Exceptions;
using ChartEngine.Infrastructure.BackgroundJobs;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly IDatasetRepository _datasetRepository;
    private readonly IExportRepository _exportRepository;
    private readonly ExportProcessingChannel _channel;
    private readonly ILogger<ExportService> _logger;
    private readonly string _storageBasePath;

    public ExportService(
        IDatasetRepository datasetRepository,
        IExportRepository exportRepository,
        ExportProcessingChannel channel,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<ExportService> logger)
    {
        _datasetRepository = datasetRepository;
        _exportRepository = exportRepository;
        _channel = channel;
        _logger = logger;

        _storageBasePath = configuration["Storage:BasePath"]
            ?? throw new InvalidOperationException("Storage:BasePath is not configured.");
    }

    public async Task<ExportJobStatusDto> StartExportAsync(
        Guid datasetId,
        string format,
        string? drillPathJson,
        CancellationToken ct = default)
    {
        var dataset = await _datasetRepository.GetByIdAsync(datasetId, ct);
        if (dataset == null)
        {
            throw new DatasetNotFoundException(datasetId);
        }

        // Validate format is either CSV or Excel
        if (!string.Equals(format, "CSV", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "Excel", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Export format must be either 'CSV' or 'Excel'.", nameof(format));
        }

        // Validate drillPath JSON if provided
        if (!string.IsNullOrWhiteSpace(drillPathJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(drillPathJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    throw new ArgumentException("drillPath must be a JSON array of filters.");
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid drillPath JSON format.", nameof(drillPathJson), ex);
            }
        }

        var job = ExportJob.Create(datasetId, format, drillPathJson);
        await _exportRepository.AddAsync(job, ct);

        bool enqueued = _channel.TryEnqueue(job.Id);
        if (!enqueued)
        {
            _logger.LogError("Failed to enqueue export job {JobId} for background execution", job.Id);
            job.MarkAsFailed("Failed to enqueue job in background worker queue.");
            await _exportRepository.UpdateAsync(job, ct);
        }

        return MapToDto(job);
    }

    public async Task<ExportJobStatusDto?> GetStatusAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _exportRepository.GetByIdAsync(jobId, ct);
        if (job == null) return null;
        return MapToDto(job);
    }

    public async Task<(Stream FileStream, string FileName, string ContentType)> DownloadExportAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _exportRepository.GetByIdAsync(jobId, ct);
        if (job == null)
        {
            throw new KeyNotFoundException($"Export job {jobId} not found.");
        }

        if (!string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Export job {jobId} is in {job.Status} state and cannot be downloaded yet.");
        }

        var filePath = job.DownloadPath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException($"Export file not found on disk at '{filePath}'.");
        }

        var isExcel = string.Equals(job.Format, "Excel", StringComparison.OrdinalIgnoreCase);
        var contentType = isExcel 
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" 
            : "text/csv";
        var fileName = isExcel ? $"export_{jobId}.xlsx" : $"export_{jobId}.csv";

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        return (stream, fileName, contentType);
    }

    public async Task ProcessExportAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _exportRepository.GetByIdAsync(jobId, ct);
        if (job == null)
        {
            _logger.LogError("Export job {JobId} not found in database for background processing", jobId);
            return;
        }

        job.MarkAsProcessing();
        await _exportRepository.UpdateAsync(job, ct);

        try
        {
            var dataset = await _datasetRepository.GetByIdAsync(job.DatasetId, ct);
            if (dataset == null)
            {
                throw new DatasetNotFoundException(job.DatasetId);
            }

            var sourceCsvPath = dataset.StoragePath;
            if (!File.Exists(sourceCsvPath))
            {
                throw new FileNotFoundException($"Source CSV file not found at '{sourceCsvPath}' for dataset {job.DatasetId}");
            }

            // Create exports subfolder in base directory
            var exportsDir = Path.Combine(_storageBasePath, "exports");
            Directory.CreateDirectory(exportsDir);

            var isExcel = string.Equals(job.Format, "Excel", StringComparison.OrdinalIgnoreCase);
            var extension = isExcel ? "xlsx" : "csv";
            var targetFilePath = Path.Combine(exportsDir, $"{jobId}.{extension}");

            List<DrillStep>? drillSteps = null;
            if (!string.IsNullOrWhiteSpace(job.DrillPath))
            {
                drillSteps = JsonSerializer.Deserialize<List<DrillStep>>(
                    job.DrillPath,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                TrimOptions = TrimOptions.None
            };

            using var streamReader = new StreamReader(sourceCsvPath, System.Text.Encoding.UTF8, true, 65536);
            using var csvReader = new CsvReader(streamReader, csvConfig);

            if (!await csvReader.ReadAsync())
            {
                throw new InvalidOperationException("Source CSV file is empty.");
            }

            csvReader.ReadHeader();
            var headers = csvReader.HeaderRecord!;

            // Prepare filters
            var preparedFilters = new List<PreparedFilter>();
            if (drillSteps != null)
            {
                foreach (var step in drillSteps)
                {
                    if (string.IsNullOrWhiteSpace(step.Column)) continue;

                    bool isHist = step.Column.StartsWith("__hist__", StringComparison.OrdinalIgnoreCase);
                    string actualColName = isHist ? step.Column.Substring(8) : step.Column;

                    int colIdx = Array.FindIndex(headers, h => string.Equals(h, actualColName, StringComparison.OrdinalIgnoreCase));
                    var filter = new PreparedFilter
                    {
                        ColumnIndex = colIdx,
                        ColumnName = actualColName,
                        IsHistogram = isHist,
                        RawValue = step.Value
                    };

                    if (isHist)
                    {
                        if (TryParseRange(step.Value, out double min, out bool minInc, out double max, out bool maxInc))
                        {
                            filter.Min = min;
                            filter.MinInclusive = minInc;
                            filter.Max = max;
                            filter.MaxInclusive = maxInc;
                        }
                        else
                        {
                            filter.ColumnIndex = -1; // Force mismatch
                        }
                    }

                    preparedFilters.Add(filter);
                }
            }

            // Write matching rows in a streaming fashion to prevent holding millions of rows in memory
            if (isExcel)
            {
                // MiniExcel natively supports deferred execution (IEnumerable with yield return)
                var rowsEnumerable = StreamFilteredRows(csvReader, headers, preparedFilters, ct);
                MiniExcel.SaveAs(targetFilePath, rowsEnumerable, overwriteFile: true);
            }
            else
            {
                // Stream filter directly to target CSV file using CsvHelper
                using var streamWriter = new StreamWriter(targetFilePath, false, System.Text.Encoding.UTF8, 65536);
                using var csvWriter = new CsvWriter(streamWriter, csvConfig);

                foreach (var header in headers)
                {
                    csvWriter.WriteField(header);
                }
                await csvWriter.NextRecordAsync();

                while (await csvReader.ReadAsync())
                {
                    ct.ThrowIfCancellationRequested();

                    if (RowMatches(csvReader, preparedFilters))
                    {
                        for (int i = 0; i < headers.Length; i++)
                        {
                            csvWriter.WriteField(csvReader.GetField(i));
                        }
                        await csvWriter.NextRecordAsync();
                    }
                }
            }

            job.MarkAsCompleted(targetFilePath);
            await _exportRepository.UpdateAsync(job, ct);
            _logger.LogInformation("Export job {JobId} successfully processed and saved to disk", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export job {JobId} failed during execution", jobId);
            job.MarkAsFailed(ex.Message);
            await _exportRepository.UpdateAsync(job, ct);
        }
    }

    private static IEnumerable<Dictionary<string, string>> StreamFilteredRows(
        CsvReader csvReader,
        string[] headers,
        List<PreparedFilter> preparedFilters,
        CancellationToken ct)
    {
        // Must be synchronous looping since MiniExcel pulls values synchronously
        while (csvReader.Read())
        {
            ct.ThrowIfCancellationRequested();

            if (RowMatches(csvReader, preparedFilters))
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    row[header] = csvReader.GetField(header) ?? string.Empty;
                }
                yield return row;
            }
        }
    }

    private static bool RowMatches(CsvReader csv, List<PreparedFilter> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.ColumnIndex < 0) return false;

            string rowVal = csv.GetField(filter.ColumnIndex) ?? string.Empty;

            if (filter.IsHistogram)
            {
                if (!double.TryParse(rowVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                {
                    return false;
                }

                bool minMatch = filter.MinInclusive ? (d >= filter.Min) : (d > filter.Min);
                bool maxMatch = filter.MaxInclusive ? (d <= filter.Max) : (d < filter.Max);

                if (!minMatch || !maxMatch) return false;
            }
            else
            {
                if (!string.Equals(rowVal.Trim(), filter.RawValue.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryParseRange(
        string value,
        out double min,
        out bool minInclusive,
        out double max,
        out bool maxInclusive)
    {
        min = 0;
        max = 0;
        minInclusive = true;
        maxInclusive = true;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string clean = value.Trim();
        if (clean.StartsWith('['))
        {
            minInclusive = true;
            clean = clean.Substring(1);
        }
        else if (clean.StartsWith('('))
        {
            minInclusive = false;
            clean = clean.Substring(1);
        }

        if (clean.EndsWith(']'))
        {
            maxInclusive = true;
            clean = clean.Substring(0, clean.Length - 1);
        }
        else if (clean.EndsWith(')'))
        {
            maxInclusive = false;
            clean = clean.Substring(0, clean.Length - 1);
        }

        clean = clean.Trim();

        string[] parts;
        if (clean.Contains(','))
        {
            parts = clean.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
        else if (clean.Contains('_'))
        {
            parts = clean.Split('_', StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            int hyphenIdx = -1;
            for (int i = 1; i < clean.Length; i++)
            {
                if (clean[i] == '-' && clean[i - 1] != '-')
                {
                    hyphenIdx = i;
                    break;
                }
            }

            if (hyphenIdx != -1)
            {
                parts = new[]
                {
                    clean.Substring(0, hyphenIdx),
                    clean.Substring(hyphenIdx + 1)
                };
            }
            else
            {
                parts = clean.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        if (parts.Length == 2)
        {
            if (double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out min) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out max))
            {
                return true;
            }
        }

        return false;
    }

    private static ExportJobStatusDto MapToDto(ExportJob job)
    {
        var downloadUrl = string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            ? $"/api/v1/datasets/exports/{job.Id}/download"
            : null;

        return new ExportJobStatusDto(
            JobId: job.Id,
            DatasetId: job.DatasetId,
            Format: job.Format,
            Status: job.Status,
            CreatedAt: job.CreatedAt,
            DownloadUrl: downloadUrl,
            ErrorMessage: job.ErrorMessage
        );
    }

    private class PreparedFilter
    {
        public int ColumnIndex { get; set; }
        public string ColumnName { get; set; } = string.Empty;
        public bool IsHistogram { get; set; }
        public string RawValue { get; set; } = string.Empty;

        public double Min { get; set; }
        public bool MinInclusive { get; set; }
        public double Max { get; set; }
        public bool MaxInclusive { get; set; }
    }

    private class DrillStep
    {
        public string Column { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
