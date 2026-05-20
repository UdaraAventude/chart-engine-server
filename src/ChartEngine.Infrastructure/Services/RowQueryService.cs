using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Exceptions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
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

public class RowQueryService : IRowQueryService
{
    private readonly IDatasetRepository _datasetRepository;
    private readonly ILogger<RowQueryService> _logger;

    public RowQueryService(IDatasetRepository datasetRepository, ILogger<RowQueryService> logger)
    {
        _datasetRepository = datasetRepository;
        _logger = logger;
    }

    public async Task<PagedRowsDto?> GetPagedRowsAsync(
        Guid datasetId,
        int page,
        int pageSize,
        string? drillPathJson,
        CancellationToken ct = default)
    {
        var dataset = await _datasetRepository.GetByIdAsync(datasetId, ct);
        if (dataset == null)
        {
            throw new DatasetNotFoundException(datasetId);
        }

        var storagePath = dataset.StoragePath;
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"CSV file not found for dataset {datasetId} at {storagePath}");
        }

        // Parse the drill path JSON if provided
        List<DrillStep>? drillSteps = null;
        if (!string.IsNullOrWhiteSpace(drillPathJson))
        {
            try
            {
                drillSteps = JsonSerializer.Deserialize<List<DrillStep>>(
                    drillPathJson, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse drillPathJson for dataset {DatasetId}", datasetId);
                throw new ArgumentException("Invalid drillPath JSON format.", nameof(drillPathJson), ex);
            }
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.None
        };

        // Open StreamReader with 64KB buffer for high performance
        using var streamReader = new StreamReader(storagePath, System.Text.Encoding.UTF8, true, 65536);
        using var csv = new CsvReader(streamReader, config);

        if (!await csv.ReadAsync())
        {
            return new PagedRowsDto
            {
                Rows = new(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord!;

        // Prepare the filters to avoid string lookups and regex execution in the hot loop
        var preparedFilters = new List<PreparedFilter>();
        if (drillSteps != null)
        {
            foreach (var step in drillSteps)
            {
                if (string.IsNullOrWhiteSpace(step.Column)) continue;

                bool isHist = step.Column.StartsWith("__hist__", StringComparison.OrdinalIgnoreCase);
                string actualColName = isHist ? step.Column.Substring(8) : step.Column;

                int colIdx = Array.FindIndex(headers, h => string.Equals(h, actualColName, StringComparison.OrdinalIgnoreCase));
                if (colIdx < 0)
                {
                    // Column name not found in the CSV headers. Since it can't match, we set index to -1.
                    _logger.LogWarning("Drill path column '{Column}' not found in dataset {DatasetId}", actualColName, datasetId);
                }

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
                        _logger.LogWarning("Failed to parse histogram range from value '{Value}'", step.Value);
                        filter.ColumnIndex = -1; // Force mismatch
                    }
                }

                preparedFilters.Add(filter);
            }
        }

        int matchCount = 0;
        int startIdx = (page - 1) * pageSize;
        int endIdx = startIdx + pageSize;
        var pagedRows = new List<Dictionary<string, string>>();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            bool matchesAll = true;
            foreach (var filter in preparedFilters)
            {
                if (filter.ColumnIndex < 0)
                {
                    matchesAll = false;
                    break;
                }

                string rowVal = csv.GetField(filter.ColumnIndex) ?? string.Empty;

                if (filter.IsHistogram)
                {
                    if (!double.TryParse(rowVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                    {
                        matchesAll = false;
                        break;
                    }

                    bool minMatch = filter.MinInclusive ? (d >= filter.Min) : (d > filter.Min);
                    bool maxMatch = filter.MaxInclusive ? (d <= filter.Max) : (d < filter.Max);

                    if (!minMatch || !maxMatch)
                    {
                        matchesAll = false;
                        break;
                    }
                }
                else
                {
                    if (!string.Equals(rowVal.Trim(), filter.RawValue.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        matchesAll = false;
                        break;
                    }
                }
            }

            if (matchesAll)
            {
                if (matchCount >= startIdx && matchCount < endIdx)
                {
                    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var header in headers)
                    {
                        row[header] = csv.GetField(header) ?? string.Empty;
                    }
                    pagedRows.Add(row);
                }
                matchCount++;
            }
        }

        return new PagedRowsDto
        {
            Rows = pagedRows,
            TotalCount = matchCount,
            Page = page,
            PageSize = pageSize
        };
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

    public class DrillStep
    {
        public string Column { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
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
}
