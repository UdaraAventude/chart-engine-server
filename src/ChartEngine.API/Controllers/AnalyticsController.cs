using ChartEngine.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChartEngine.API.Controllers;

[ApiController]
[Route("api/v1/datasets")]
public class AnalyticsController : ControllerBase
{
    private readonly IRowQueryService _rowQueryService;

    public AnalyticsController(IRowQueryService rowQueryService)
    {
        _rowQueryService = rowQueryService;
    }

    [HttpGet("{datasetId:guid}/rows")]
    public async Task<IActionResult> GetPagedRowsAsync(
        Guid datasetId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? drillPath = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100; // Limit max page size

        try
        {
            var result = await _rowQueryService.GetPagedRowsAsync(datasetId, page, pageSize, drillPath, ct);
            if (result is null)
            {
                return NotFound(new { error = $"Dataset {datasetId} not found." });
            }
            return Ok(result);
        }
        catch (ChartEngine.Domain.Exceptions.DatasetNotFoundException)
        {
            return NotFound(new { error = $"Dataset {datasetId} not found." });
        }
        catch (System.IO.FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
