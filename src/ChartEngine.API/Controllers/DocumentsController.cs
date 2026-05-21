using ChartEngine.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ChartEngine.API.Controllers;

[ApiController]
[Route("api/v1/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IUploadProcessingService _uploadService;
    private readonly IVisualizationService _visualizationService;
    private readonly ChartEngine.Application.Interfaces.Repositories.IDatasetRepository _datasetRepository;
    private readonly ChartEngine.Application.Interfaces.Repositories.ITreeRepository _treeRepository;
    private readonly ChartEngine.Application.Interfaces.Services.ISampleRowsService _sampleRowsService;

    public DocumentsController(
        IUploadProcessingService uploadService, 
        IVisualizationService visualizationService,
        ChartEngine.Application.Interfaces.Repositories.IDatasetRepository datasetRepository,
        ChartEngine.Application.Interfaces.Repositories.ITreeRepository treeRepository,
        ChartEngine.Application.Interfaces.Services.ISampleRowsService sampleRowsService)
    {
        _uploadService = uploadService;
        _visualizationService = visualizationService;
        _datasetRepository = datasetRepository;
        _treeRepository = treeRepository;
        _sampleRowsService = sampleRowsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPagedAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var result = await _datasetRepository.GetPagedDatasetsAsync(page, pageSize, sortBy, search, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken ct)
    {
        var dataset = await _datasetRepository.GetByIdAsync(id, ct);
        if (dataset == null)
            return NotFound(new { error = $"Dataset {id} not found." });

        await _datasetRepository.DeleteAsync(dataset, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/metadata")]
    public async Task<IActionResult> GetMetadataAsync(Guid id, CancellationToken ct = default)
    {
        var dataset = await _datasetRepository.GetByIdAsync(id, ct);
        if (dataset == null)
            return NotFound(new { error = $"Dataset {id} not found." });

        var envelope = await _treeRepository.GetEnvelopeMetadataAsync(id, ct);
        if (envelope == null)
            return NotFound(new { error = $"Metadata for dataset {id} not found." });

        var config = await _datasetRepository.GetConfigAsync(id, ct);
        var maxHierarchyDepth = envelope.MaxHierarchyDepth;
        if (maxHierarchyDepth <= 0 || maxHierarchyDepth > envelope.Dimensions.Count)
            maxHierarchyDepth = config?.MaxHierarchyDepth ?? envelope.MaxHierarchyDepth;
        if (maxHierarchyDepth <= 0)
            maxHierarchyDepth = Math.Min(
                envelope.Dimensions.Count,
                envelope.TotalRows > 1_000_000 ? 4 : envelope.Dimensions.Count);

        var dto = new ChartEngine.Application.DTOs.DatasetMetadataDto(
            dataset.Id,
            dataset.FileName,
            dataset.Status.ToString(),
            dataset.TotalRows > 0 ? dataset.TotalRows : envelope.TotalRows,
            envelope.Dimensions,
            envelope.Metrics,
            envelope.Rejected,
            maxHierarchyDepth);

        return Ok(dto);
    }

    [HttpGet("{id:guid}/sample-rows")]
    public async Task<IActionResult> GetSampleRows(
        Guid id,
        [FromQuery] int limit = 5000,
        [FromQuery] string? drillPath = null,
        CancellationToken ct = default)
    {
        var result = await _sampleRowsService.GetSampleRowsAsync(id, drillPath, limit, ct);
        return Ok(result);
    }

    [HttpGet("visual")]
    public async Task<IActionResult> GetVisualization(
        [FromQuery] Guid id, 
        [FromQuery] string chartType = "bar", 
        [FromQuery] int drillDown = 0, 
        [FromQuery] string aggregation = "count",
        [FromQuery] string? drillPath = null)
    {
        var result = await _visualizationService.GetVisualizationAsync(
            id, chartType, drillDown, aggregation, drillPath);
        return Ok(result);
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task Upload(IFormFile file)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no"); // Prevent NGINX/Proxies from buffering

        try 
        {
            var result = await _uploadService.ProcessAsync(file, async (progress) => 
            {
                var payload = JsonSerializer.Serialize(new { progress, status = "processing" });
                await Response.WriteAsync($"data: {payload}\n\n");
                await Response.Body.FlushAsync();
            });

            var finalPayload = JsonSerializer.Serialize(new { 
                progress = 100, 
                status = "completed", 
                data = result 
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            
            await Response.WriteAsync($"data: {finalPayload}\n\n");
        }
        catch (Exception ex)
        {
            var errorPayload = JsonSerializer.Serialize(new { 
                progress = 0, 
                status = "error", 
                message = ex.Message 
            });
            await Response.WriteAsync($"data: {errorPayload}\n\n");
        }
        finally
        {
            await Response.Body.FlushAsync();
        }
    }
}
