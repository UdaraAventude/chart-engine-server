using ChartEngine.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace ChartEngine.Application.Interfaces.Services;

public interface IUploadProcessingService
{
    Task<UploadCompletedResult> ProcessAsync(IFormFile file, Func<int, Task> onProgressAsync);
}
