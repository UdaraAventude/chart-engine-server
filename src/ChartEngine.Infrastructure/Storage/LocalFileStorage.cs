namespace ChartEngine.Infrastructure.Storage;

using ChartEngine.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IConfiguration configuration)
    {
        
        
        _basePath = configuration["Storage:BasePath"]
            ?? throw new InvalidOperationException("Storage:BasePath is not configured.");

        
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(
        Guid datasetId,
        Stream content,
        string originalFileName,
        CancellationToken ct = default)
    {
        
        var fileName = $"{datasetId}.csv";
        var fullPath = Path.Combine(_basePath, fileName);

        
        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,  
            useAsync: true);

        await content.CopyToAsync(fileStream, ct);

        return fullPath;
    }

    public Stream OpenRead(string storagePath)
    {
        if (!File.Exists(storagePath))
            throw new FileNotFoundException($"Dataset file not found at: {storagePath}");

        return new FileStream(
            storagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
    }

    public async Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (File.Exists(storagePath))
            await Task.Run(() => File.Delete(storagePath), ct);
    }
}

