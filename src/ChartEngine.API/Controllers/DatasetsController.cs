namespace ChartEngine.API.Controllers;

using ChartEngine.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/datasets")]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetService _datasetService;

    // The controller receives the SERVICE INTERFACE — not the implementation
    // This is dependency injection at work
    public DatasetsController(IDatasetService datasetService)
    {
        _datasetService = datasetService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)]  // 500MB max
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        CancellationToken ct)
    {
        // Controllers only do three things:
        // 1. Receive the HTTP request
        // 2. Call the service
        // 3. Return an HTTP response

        var result = await _datasetService.UploadAsync(file, ct);

        // 202 Accepted = "I received it, processing is happening, check back later"
        // (better than 200 OK which implies the work is done)
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
