namespace ChartEngine.API.Controllers;

using ChartEngine.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/datasets")]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetService _datasetService;

    
    
    public DatasetsController(IDatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit] 
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = 2_147_483_647)] 
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        CancellationToken ct)
    {
        
        
        
        

        var result = await _datasetService.UploadAsync(file, ct);

        
        
        return Accepted(new
        {
            datasetId = result.DatasetId,
            status = result.Status,
            fileName = result.FileName
        });
    }

    [HttpGet("{datasetId:guid}/status")]
    public async Task<IActionResult> GetStatusAsync(
        Guid datasetId,
        CancellationToken ct)
    {
        var status = await _datasetService.GetStatusAsync(datasetId, ct);
        return Ok(status);
    }

    [HttpGet("{datasetId:guid}/tree")]
    public async Task<IActionResult> GetTreeAsync(
        Guid datasetId,
        CancellationToken ct)
    {
        try
        {
            var treeJson = await _datasetService.GetTreeJsonAsync(datasetId, ct);
            
            if (string.IsNullOrEmpty(treeJson))
            {
                return NotFound(new { error = "Tree not found or empty." });
            }

            // Return the raw JSON string as application/json
            return Content(treeJson, "application/json");
        }
        catch (ChartEngine.Domain.Exceptions.DatasetNotFoundException)
        {
            return NotFound(new { error = $"Dataset {datasetId} not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{datasetId:guid}/schema")]
    public async Task<IActionResult> GetSchemaAsync(
        Guid datasetId,
        CancellationToken ct)
    {
        try
        {
            var schema = await _datasetService.GetSchemaAsync(datasetId, ct);
            return Ok(schema);
        }
        catch (ChartEngine.Domain.Exceptions.DatasetNotFoundException)
        {
            return NotFound(new { error = $"Dataset {datasetId} not found." });
        }
    }

    [HttpGet("{datasetId:guid}/drill")]
    public async Task<IActionResult> GetDrillDownAsync(
        Guid datasetId,
        [FromQuery] string[]? path,
        CancellationToken ct)
    {
        try
        {
            var branch = await _datasetService.GetDrillDownAsync(datasetId, path, ct);
            if (branch is null)
            {
                return NotFound(new { error = "Path not found or dataset not ready." });
            }
            return Ok(branch);
        }
        catch (ChartEngine.Domain.Exceptions.DatasetNotFoundException)
        {
            return NotFound(new { error = $"Dataset {datasetId} not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

