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

    public DocumentsController(IUploadProcessingService uploadService)
    {
        _uploadService = uploadService;
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
