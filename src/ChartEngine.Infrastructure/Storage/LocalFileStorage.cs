namespace ChartEngine.Infrastructure.Storage;

using ChartEngine.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Configuration;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IConfiguration configuration)
    {
        // Read the base path from appsettings.json
        // We'll configure this in Step 7
        _basePath = configuration["Storage:BasePath"]
            ?? throw new InvalidOperationException("Storage:BasePath is not configured.");

        // Ensure the directory exists when the app starts
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(
        Guid datasetId,
        Stream content,
        string originalFileName,
        CancellationToken ct = default)
    {
        // Build a stable, unique path for this dataset's CSV
        var fileName = $"{datasetId}.csv";
        var fullPath = Path.Combine(_basePath, fileName);

        // Stream the upload directly to disk — don't load the whole file into memory
        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,  // 80KB buffer — efficient for large files
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
