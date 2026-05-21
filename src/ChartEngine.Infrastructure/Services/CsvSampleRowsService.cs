using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Domain.Exceptions;
using ChartEngine.Infrastructure.Analytics;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ChartEngine.Infrastructure.Services;

public class CsvSampleRowsService : ISampleRowsService
{
    private const int MaxLimit = 10_000;
    private readonly IDatasetRepository _datasetRepository;
    private readonly ITreeRepository _treeRepository;

    public CsvSampleRowsService(
        IDatasetRepository datasetRepository,
        ITreeRepository treeRepository)
    {
        _datasetRepository = datasetRepository;
        _treeRepository = treeRepository;
    }

    public async Task<SampleRowsDto> GetSampleRowsAsync(
        Guid datasetId,
        string? drillPathJson,
        int limit = 5000,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, MaxLimit);

        var dataset = await _datasetRepository.GetByIdAsync(datasetId, ct)
            ?? throw new DatasetNotFoundException(datasetId);

        var envelope = await _treeRepository.GetEnvelopeMetadataAsync(datasetId, ct)
            ?? throw new KeyNotFoundException($"Metadata for dataset {datasetId} not found.");

        if (!File.Exists(dataset.StoragePath))
            throw new FileNotFoundException($"Source file not found for dataset {datasetId}.");

        var drillSteps = TreeNavigator.ParseDrillPathJson(drillPathJson);
        var rows = new List<Dictionary<string, string>>();

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var reader = new StreamReader(dataset.StoragePath, System.Text.Encoding.UTF8, true, 65536);
        using var csv = new CsvReader(reader, csvConfig);

        if (!await csv.ReadAsync())
            return new SampleRowsDto { Dimensions = envelope.Dimensions, Metrics = envelope.Metrics };

        csv.ReadHeader();
        var headers = csv.HeaderRecord!;
        var filters = DrillPathCsvFilter.Build(headers, drillSteps);

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            if (!DrillPathCsvFilter.RowMatches(csv, filters)) continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
                row[header] = csv.GetField(header) ?? string.Empty;

            rows.Add(row);
            if (rows.Count >= limit) break;
        }

        return new SampleRowsDto
        {
            Rows = rows,
            Dimensions = envelope.Dimensions,
            Metrics = envelope.Metrics,
        };
    }
}

internal static class DrillPathCsvFilter
{
    internal sealed class PreparedFilter
    {
        public int ColumnIndex { get; set; } = -1;
        public bool IsHistogram { get; set; }
        public string RawValue { get; set; } = string.Empty;
        public double Min { get; set; }
        public bool MinInclusive { get; set; }
        public double Max { get; set; }
        public bool MaxInclusive { get; set; }
    }

    public static List<PreparedFilter> Build(string[] headers, IReadOnlyList<DrillPathStepDto> steps)
    {
        var filters = new List<PreparedFilter>();
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Column)) continue;

            var isHist = step.Column.StartsWith("__hist__", StringComparison.OrdinalIgnoreCase);
            var colName = isHist ? step.Column[8..] : step.Column;
            var colIdx = Array.FindIndex(headers, h => string.Equals(h, colName, StringComparison.OrdinalIgnoreCase));

            var filter = new PreparedFilter
            {
                ColumnIndex = colIdx,
                IsHistogram = isHist,
                RawValue = step.Value ?? string.Empty,
            };

            if (isHist && TryParseRange(step.Value, out var min, out var minInc, out var max, out var maxInc))
            {
                filter.Min = min;
                filter.MinInclusive = minInc;
                filter.Max = max;
                filter.MaxInclusive = maxInc;
            }
            else if (isHist)
            {
                filter.ColumnIndex = -1;
            }

            filters.Add(filter);
        }

        return filters;
    }

    public static bool RowMatches(CsvReader csv, List<PreparedFilter> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.ColumnIndex < 0) return false;
            var rowVal = csv.GetField(filter.ColumnIndex) ?? string.Empty;

            if (filter.IsHistogram)
            {
                if (!double.TryParse(rowVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    return false;
                var minOk = filter.MinInclusive ? d >= filter.Min : d > filter.Min;
                var maxOk = filter.MaxInclusive ? d <= filter.Max : d < filter.Max;
                if (!minOk || !maxOk) return false;
            }
            else if (!string.Equals(rowVal.Trim(), filter.RawValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseRange(
        string? label,
        out double min,
        out bool minInclusive,
        out double max,
        out bool maxInclusive)
    {
        min = 0;
        max = 0;
        minInclusive = true;
        maxInclusive = false;

        if (string.IsNullOrWhiteSpace(label)) return false;

        var match = Regex.Match(label.Trim(), @"^([\d.kM]+)\s*(≤|<)\s*x\s*(≤|<)\s*([\d.kM]+)$");
        if (!match.Success) return false;

        min = ParseMagnitude(match.Groups[1].Value);
        max = ParseMagnitude(match.Groups[4].Value);
        minInclusive = match.Groups[2].Value == "≤";
        maxInclusive = match.Groups[3].Value == "≤";
        return true;
    }

    private static double ParseMagnitude(string s)
    {
        s = s.Trim();
        if (s.EndsWith("M", StringComparison.OrdinalIgnoreCase))
            return double.Parse(s[..^1], CultureInfo.InvariantCulture) * 1_000_000;
        if (s.EndsWith("k", StringComparison.OrdinalIgnoreCase))
            return double.Parse(s[..^1], CultureInfo.InvariantCulture) * 1_000;
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
}
