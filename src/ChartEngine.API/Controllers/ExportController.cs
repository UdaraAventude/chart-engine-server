using ChartEngine.Application.DTOs;
using ChartEngine.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.API.Controllers;

[ApiController]
[Route("api/v1/datasets")]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpPost("{datasetId:guid}/export")]
    public async Task<IActionResult> ExportAsync(
        Guid datasetId,
        [FromBody] ExportRequest request,
        CancellationToken ct)
    {
        try
        {
            string? drillPathJson = null;
            if (request.DrillPath.HasValue && request.DrillPath.Value.ValueKind != JsonValueKind.Null)
            {
                if (request.DrillPath.Value.ValueKind == JsonValueKind.String)
                {
                    drillPathJson = request.DrillPath.Value.GetString();
                }
                else
                {
                    drillPathJson = request.DrillPath.Value.GetRawText();
                }
            }

            var jobStatus = await _exportService.StartExportAsync(datasetId, request.Format, drillPathJson, ct);
            return Accepted(jobStatus);
        }
        catch (ChartEngine.Domain.Exceptions.DatasetNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("exports/{jobId:guid}")]
    public async Task<IActionResult> GetStatusAsync(
        Guid jobId,
        CancellationToken ct)
    {
        var jobStatus = await _exportService.GetStatusAsync(jobId, ct);
        if (jobStatus == null)
        {
            return NotFound(new { error = $"Export job {jobId} not found." });
        }
        return Ok(jobStatus);
    }

    [HttpGet("exports/{jobId:guid}/download")]
    public async Task<IActionResult> DownloadAsync(
        Guid jobId,
        CancellationToken ct)
    {
        try
        {
            var (stream, fileName, contentType) = await _exportService.DownloadExportAsync(jobId, ct);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class ExportRequest
{
    public string Format { get; set; } = "CSV";
    public JsonElement? DrillPath { get; set; }
}
