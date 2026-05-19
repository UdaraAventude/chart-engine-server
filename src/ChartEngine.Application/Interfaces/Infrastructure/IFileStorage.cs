namespace ChartEngine.Application.Interfaces.Infrastructure;

public interface IFileStorage
{
    // Saves a stream to storage, returns the path where it was saved
    Task<string> SaveAsync(Guid datasetId, Stream content, string originalFileName, CancellationToken ct = default);
    
    // Opens a saved file for reading
    Stream OpenRead(string storagePath);
    
    // Deletes a file (for cleanup on failed uploads)
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
