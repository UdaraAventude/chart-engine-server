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
    [RequestSizeLimit(500_000_000)]  
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
}

