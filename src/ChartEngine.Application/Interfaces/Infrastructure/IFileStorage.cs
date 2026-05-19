namespace ChartEngine.Application.Interfaces.Infrastructure;

public interface IFileStorage
{
    
    Task<string> SaveAsync(Guid datasetId, Stream content, string originalFileName, CancellationToken ct = default);
    
    
    Stream OpenRead(string storagePath);
    
    
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}

