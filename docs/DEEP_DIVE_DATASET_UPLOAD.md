# Deep Dive: Dataset Upload & Ingestion

This document provides a **code-level walkthrough** of the dataset upload feature, including exact file paths, line numbers, method signatures, and a step-by-step tracing guide.

---

## Overview: Request Flow

```
Client (HTTP POST)
    ↓
DatasetsController.UploadAsync()
    ↓
IDatasetService.UploadAsync() [interface]
    ↓
DatasetService.UploadAsync() [implementation]
    ↓
File Storage + Repository (persists Dataset entity)
    ↓
Enqueue to DatasetProcessingChannel
    ↓
DatasetProcessingWorker (background)
    ↓
IDatasetPipelineService.ProcessAsync() [orchestration]
    ↓
Schema Detection + Tree Building + Persistence
    ↓
SignalR Hub Notifications to Client
```

---

## 1. HTTP Entry Point: Controller

### File: [src/ChartEngine.API/Controllers/DatasetsController.cs](src/ChartEngine.API/Controllers/DatasetsController.cs)

**Endpoint Definition (Lines 20-38):**

```csharp
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
```

**What happens:**

- Route: `POST /api/v1/datasets/upload`
- Accepts an `IFormFile` (HTTP multipart/form-data upload)
- **Immediately returns HTTP 202 Accepted** with `DatasetUploadResult` (dataset ID, initial status "Pending", file name)
- Processing happens **asynchronously in the background**

**Constructor (Lines 13-17):**

```csharp
public DatasetsController(IDatasetService datasetService)
{
    _datasetService = datasetService;
}
```

Injects `IDatasetService` (interface) — implementation resolved by DI container.

---

## 2. Application Service: Upload Logic

### File: [src/ChartEngine.Infrastructure/Services/DatasetService.cs](src/ChartEngine.Infrastructure/Services/DatasetService.cs)

**Service Interface (from IDatasetService, lines 6-7):**

```csharp
public interface IDatasetService
{
    Task<DatasetUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default);
    // ... other methods ...
}
```

**Implementation: UploadAsync (Lines 36-68):**

```csharp
public async Task<DatasetUploadResult> UploadAsync(IFormFile file, CancellationToken ct = default)
{
    // Step 1: Create domain entity
    var dataset = Dataset.Create(
        fileName: file.FileName,
        fileSizeBytes: file.Length,
        storagePath: string.Empty
    );

    // Step 2: Save file to blob storage
    await using var stream = file.OpenReadStream();
    var storagePath = await _fileStorage.SaveAsync(dataset.Id, stream, file.FileName, ct);

    // Step 3: Update storage path in entity
    dataset.SetStoragePath(storagePath);

    // Step 4: Persist dataset to DB (status = "Pending")
    await _repository.AddAsync(dataset, ct);

    // Step 5: Enqueue for background processing
    var enqueued = _channel.TryEnqueue(dataset.Id);
    if (!enqueued)
        _logger.LogWarning("Failed to enqueue dataset {DatasetId} for processing", dataset.Id);

    // Step 6: Return immediately to client
    return new DatasetUploadResult(
        DatasetId: dataset.Id,
        Status: dataset.Status.ToString(),
        FileName: dataset.FileName
    );
}
```

**Detailed breakdown:**

1. **Line 39-44:** Domain entity creation via factory method
2. **Line 46-48:** File is read and saved to blob storage (IFileStorage)
   - Returns `storagePath` (e.g., `datasets/{datasetId}/filename.csv`)
3. **Line 51:** Update dataset entity with storage path
4. **Line 54:** Persist to database via `IDatasetRepository.AddAsync()`
5. **Line 57-59:** Enqueue dataset ID to background processing channel
6. **Line 61-65:** Return result record (DTO)

---

## 3. Domain Entity: Dataset

### File: [src/ChartEngine.Domain/Entities/Dataset.cs](src/ChartEngine.Domain/Entities/Dataset.cs)

**Entity Structure (Lines 1-54):**

```csharp
public class Dataset
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FileName { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public int TotalRows { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public DatasetStatus Status { get; private set; } = DatasetStatus.Pending;
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; private set; }

    private Dataset() { }  // Private ctor for EF Core

    public static Dataset Create(string fileName, long fileSizeBytes, string storagePath)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        if (fileSizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(fileSizeBytes));

        return new Dataset
        {
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            StoragePath = storagePath
        };
    }

    public void MarkAsProcessing()
    {
        if (Status != DatasetStatus.Pending)
            throw new InvalidOperationException($"Cannot start processing a dataset in {Status} state.");
        Status = DatasetStatus.Processing;
    }

    public void MarkAsReady(int totalRows)
    {
        if (Status != DatasetStatus.Processing)
            throw new InvalidOperationException($"Cannot mark as ready a dataset in {Status} state.");
        Status = DatasetStatus.Ready;
        TotalRows = totalRows;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        Status = DatasetStatus.Failed;
        ErrorMessage = errorMessage;
        ProcessedAt = DateTime.UtcNow;
    }
}
```

**State Transitions:**

```
Pending → Processing → Ready (success)
      ↓
      → Failed (error)
```

---

## 4. Data Transfer Objects (DTOs)

### File: [src/ChartEngine.Application/DTOs/DatasetUploadResult.cs](src/ChartEngine.Application/DTOs/DatasetUploadResult.cs)

```csharp
public record DatasetUploadResult(
    Guid DatasetId,
    string Status,
    string FileName
);
```

**Returned immediately after HTTP upload.**

### File: [src/ChartEngine.Application/DTOs/DatasetStatusDto.cs](src/ChartEngine.Application/DTOs/DatasetStatusDto.cs)

Used later by `GetStatusAsync()` to check processing progress:

```csharp
public record DatasetStatusDto(
    Guid DatasetId,
    string FileName,
    string Status,           // "Pending", "Processing", "Ready", "Failed"
    int TotalRows,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? ErrorMessage
);
```

---

## 5. Persistence Layer: Repository

### File: [src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs](src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs)

**AddAsync Method (Lines 19-23):**

```csharp
public async Task AddAsync(Dataset dataset, CancellationToken ct = default)
{
    await _context.Datasets.AddAsync(dataset, ct);
    await _context.SaveChangesAsync(ct);  // EF Core: INSERT into Datasets table
}
```

**UpdateAsync Method (Lines 25-29):**

```csharp
public async Task UpdateAsync(Dataset dataset, CancellationToken ct = default)
{
    _context.Datasets.Update(dataset);
    await _context.SaveChangesAsync(ct);  // EF Core: UPDATE Datasets table
}
```

Used later by background worker to transition state (Pending → Processing → Ready/Failed).

---

## 6. Background Job Queue

### File: [src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingChannel.cs](src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingChannel.cs)

**Channel Design (Lines 7-24):**

```csharp
public sealed class DatasetProcessingChannel
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true
        });

    public bool TryEnqueue(Guid datasetId)
        => _channel.Writer.TryWrite(datasetId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
```

**How it works:**

- Thread-safe in-memory queue
- Producer: `DatasetService.UploadAsync()` calls `TryEnqueue(datasetId)`
- Consumer: `DatasetProcessingWorker` reads from channel in a loop
- Does **not busy-wait** — suspends when queue is empty, resumes when job arrives

---

## 7. Background Worker: Job Processing

### File: [src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingWorker.cs](src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingWorker.cs)

**Worker Loop (Lines 30-46):**

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("Dataset processing worker started");

    try
    {
        await foreach (var datasetId in _channel.ReadAllAsync(stoppingToken))
        {
            await ProcessOneAsync(datasetId, stoppingToken);
        }
    }
    finally
    {
        _logger.LogInformation("Dataset processing worker stopped");
    }
}
```

**ProcessOneAsync Method (Lines 48-77):**

```csharp
private async Task ProcessOneAsync(Guid datasetId, CancellationToken ct)
{
    _logger.LogInformation("Worker picked up dataset {DatasetId}", datasetId);

    // Create fresh DI scope per job (new DbContext, fresh pipeline)
    await using var scope = _scopeFactory.CreateAsyncScope();
    var pipeline = scope.ServiceProvider.GetRequiredService<IDatasetPipelineService>();

    try
    {
        await pipeline.ProcessAsync(datasetId, ct);  // Main processing logic
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Processing of dataset {DatasetId} was cancelled", datasetId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Worker caught unhandled error for dataset {DatasetId}", datasetId);
    }
}
```

**What happens:**

1. Line 35: Reads dataset ID from channel (blocks if empty)
2. Line 59: Creates new DI scope (fresh DbContext per job)
3. Line 60: Gets `IDatasetPipelineService` implementation from DI container
4. Line 64: Calls `ProcessAsync()` to perform heavy lifting

---

## 8. Pipeline Service: Main Processing

### File: [src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs](src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs)

**ProcessAsync Method (Lines 43-105):**

```csharp
public async Task ProcessAsync(Guid datasetId, CancellationToken ct = default)
{
    _logger.LogInformation("Pipeline starting for dataset {DatasetId}", datasetId);

    // Step 1: Load dataset entity
    var dataset = await _datasetRepository.GetByIdAsync(datasetId, ct)
        ?? throw new DatasetNotFoundException(datasetId);

    // Step 2: Transition to Processing state
    dataset.MarkAsProcessing();
    await _datasetRepository.UpdateAsync(dataset, ct);
    await PushProgressAsync(datasetId, 5, "Starting pipeline", ct);

    try
    {
        // Step 3: Read sample rows from file
        await PushProgressAsync(datasetId, 10, "Reading sample rows", ct);
        var sampleRows = await ReadSampleRowsAsync(dataset.StoragePath, SchemaSampleSize);

        // Step 4: Estimate total row count
        int estimatedRowCount = (int)(dataset.FileSizeBytes / 100);

        // Step 5: Detect schema from sample
        await PushProgressAsync(datasetId, 20, "Detecting schema", ct);
        var schema = _schemaDetector.Detect(sampleRows, estimatedRowCount);

        _logger.LogInformation(
            "Dataset {DatasetId}: {DimCount} dimensions, {MetricCount} metrics, {RejectCount} rejected",
            datasetId, schema.Dimensions.Count, schema.Metrics.Count, schema.Rejected.Count);

        // Step 6: Persist schema to DB
        await PersistSchemaAsync(datasetId, schema, ct);
        await PushProgressAsync(datasetId, 30, "Schema detected", ct);

        // Step 7: Build aggregation tree (main computation)
        await PushProgressAsync(datasetId, 35, "Building aggregation tree", ct);

        var (root, totalRows) = await _treeBuilder.BuildAsync(
            storagePath: dataset.StoragePath,
            schema: schema,
            onProgress: async percent =>
            {
                int mapped = 35 + (int)(percent * 0.55);
                await PushProgressAsync(datasetId, mapped, "Building tree", ct);
            },
            ct: ct);

        _logger.LogInformation(
            "Dataset {DatasetId}: tree built, {TotalRows} rows processed",
            datasetId, totalRows);

        // Step 8: Save tree and mark as Ready
        await PushProgressAsync(datasetId, 92, "Saving tree", ct);
        await _treeRepository.SaveAsync(datasetId, root, schema, totalRows, ct);

        dataset.MarkAsReady(totalRows);
        await _datasetRepository.UpdateAsync(dataset, ct);

        // Step 9: Notify client of completion
        await PushProgressAsync(datasetId, 100, "Complete", ct);
        _logger.LogInformation("Pipeline completed successfully for dataset {DatasetId}", datasetId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Pipeline failed for dataset {DatasetId}", datasetId);
        dataset.MarkAsFailed(ex.Message);
        await _datasetRepository.UpdateAsync(dataset, ct);
        await PushProgressAsync(datasetId, 0, $"Failed: {ex.Message}", ct);
    }
}
```

**Key Sub-Steps:**

**Schema Detection (ISchemaDetector):**

- Interface: [src/ChartEngine.Application/Interfaces/Analytics/ISchemaDetector.cs](src/ChartEngine.Application/Interfaces/Analytics/ISchemaDetector.cs)
- Implementation: [src/ChartEngine.Infrastructure/Analytics/SchemaDetector.cs](src/ChartEngine.Infrastructure/Analytics/SchemaDetector.cs)
- Analyzes first ~500 rows to infer column types (dimensions vs. metrics)

**Tree Building (ITreeBuilder):**

- Interface: [src/ChartEngine.Application/Interfaces/Analytics/ITreeBuilder.cs](src/ChartEngine.Application/Interfaces/Analytics/ITreeBuilder.cs)
- Implementation: [src/ChartEngine.Infrastructure/Analytics/TreeBuilder.cs](src/ChartEngine.Infrastructure/Analytics/TreeBuilder.cs)
- Reads entire file and builds multi-dimensional aggregation tree
- Supports progress callback for real-time updates

**Real-time Progress (SignalR):**

- Method `PushProgressAsync()` sends progress events to all connected clients via `IHubContext<DatasetHub>`
- Clients receive events like "Detecting schema (20%)", "Building tree (45%)", etc.

---

## 9. Real-Time Notifications: SignalR

### File: [src/ChartEngine.API/Hubs/DatasetHubEvents.cs](src/ChartEngine.API/Hubs/DatasetHubEvents.cs)

Hub events are broadcasted using `IHubContext<DatasetHub>`:

```csharp
private async Task PushProgressAsync(Guid datasetId, int percent, string message, CancellationToken ct)
{
    await _hub.Clients
        .Group($"dataset-{datasetId}")
        .SendAsync("ProgressUpdated", new { datasetId, percent, message }, cancellationToken: ct);
}
```

**Client subscription example:**

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/dataset")
  .build();

connection.on("ProgressUpdated", (data) => {
  console.log(`Dataset ${data.datasetId}: ${data.percent}% - ${data.message}`);
});

// Subscribe to dataset group
connection.invoke("JoinGroup", `dataset-${datasetId}`);
```

---

## 10. Database Schema

The uploaded `Dataset` entity persists to a table like:

```sql
CREATE TABLE Datasets (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    FileName NVARCHAR(255),
    FileSizeBytes BIGINT,
    TotalRows INT,
    StoragePath NVARCHAR(MAX),
    Status NVARCHAR(50),             -- "Pending", "Processing", "Ready", "Failed"
    ErrorMessage NVARCHAR(MAX),
    CreatedAt DATETIME2 UTC,
    ProcessedAt DATETIME2 UTC
);
```

Plus related tables:

- `DatasetColumns` — detected columns (with data types)
- `DatasetConfigs` — schema configuration
- `AggregationTrees` — computed tree structure

---

## 11. Error Handling

### Global Exception Middleware

File: [src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs](src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs)

Catches exceptions in the request pipeline and converts them to HTTP responses.

### Pipeline Exceptions

If schema detection or tree building fails:

- Exception is caught in `ProcessAsync()` (line 106+)
- `dataset.MarkAsFailed(ex.Message)` transitions state to "Failed"
- Error message is persisted
- Client is notified via SignalR

---

## 12. Step-by-Step Code Tracing Guide

### Trace Path 1: From Controller to File Storage

**Goal:** Follow how an uploaded file is stored.

**Steps:**

1. Open [src/ChartEngine.API/Controllers/DatasetsController.cs](src/ChartEngine.API/Controllers/DatasetsController.cs#L20)
   - Find method: `UploadAsync()` (line 20)
   - See call to `_datasetService.UploadAsync(file, ct)` (line 31)

2. Open [src/ChartEngine.Infrastructure/Services/DatasetService.cs](src/ChartEngine.Infrastructure/Services/DatasetService.cs#L36)
   - Find method: `UploadAsync()` (line 36)
   - See `Dataset.Create()` call (line 39-44) — creates entity
   - See `_fileStorage.SaveAsync()` call (line 48) — stores file to blob

3. `IFileStorage` is an interface. Find implementation:
   - Search repo for `class.*FileStorage` or `implements IFileStorage`
   - Typically in `src/ChartEngine.Infrastructure/Storage/`

4. Back to [src/ChartEngine.Infrastructure/Services/DatasetService.cs](src/ChartEngine.Infrastructure/Services/DatasetService.cs#L54)
   - See `_repository.AddAsync(dataset, ct)` (line 54) — saves to DB

5. Open [src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs](src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs#L19)
   - Find method: `AddAsync()` (line 19)
   - See EF Core `_context.Datasets.AddAsync()` (line 21)

### Trace Path 2: From Controller to Background Processing

**Goal:** Follow how dataset is enqueued for processing.

**Steps:**

1. [src/ChartEngine.API/Controllers/DatasetsController.cs](src/ChartEngine.API/Controllers/DatasetsController.cs#L31)
   - Controller calls `_datasetService.UploadAsync(file, ct)`

2. [src/ChartEngine.Infrastructure/Services/DatasetService.cs](src/ChartEngine.Infrastructure/Services/DatasetService.cs#L57)
   - Line 57: `_channel.TryEnqueue(dataset.Id)` — enqueues job

3. [src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingChannel.cs](src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingChannel.cs#L21)
   - Line 21: `TryEnqueue()` writes to channel

4. [src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingWorker.cs](src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingWorker.cs#L35)
   - Line 35: `await foreach (var datasetId in _channel.ReadAllAsync(stoppingToken))`
   - Reads datasetId from channel (blocks if empty)
   - Line 59: Calls `ProcessOneAsync(datasetId, stoppingToken)`

5. Line 64: `await pipeline.ProcessAsync(datasetId, ct)`
   - Calls the pipeline service

### Trace Path 3: From Pipeline to Schema Detection

**Goal:** Follow how schema is detected and where results are stored.

**Steps:**

1. [src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs](src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs#L43)
   - Line 43: `ProcessAsync()` starts

2. Line 53: `var sampleRows = await ReadSampleRowsAsync(dataset.StoragePath, SchemaSampleSize)`
   - Reads first ~500 rows from file

3. Line 61: `var schema = _schemaDetector.Detect(sampleRows, estimatedRowCount)`
   - Calls schema detector

4. Open [src/ChartEngine.Infrastructure/Analytics/SchemaDetector.cs](src/ChartEngine.Infrastructure/Analytics/SchemaDetector.cs)
   - Find method: `Detect()`
   - Analyzes column types, identifies dimensions vs. metrics, rejects invalid columns

5. Back to [src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs](src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs#L71)
   - Line 71: `await PersistSchemaAsync(datasetId, schema, ct)`
   - Saves schema (DatasetColumns) to DB

6. [src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs](src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs#L31)
   - Method: `SaveColumnsAsync()` (line 31)
   - Inserts DatasetColumns into DB

### Trace Path 4: From Pipeline to Tree Building

**Goal:** Follow the heavy computation and tree persistence.

**Steps:**

1. [src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs](src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs#L76)
   - Line 76: `var (root, totalRows) = await _treeBuilder.BuildAsync(...)`
   - Calls tree builder (main computation)

2. Open [src/ChartEngine.Infrastructure/Analytics/TreeBuilder.cs](src/ChartEngine.Infrastructure/Analytics/TreeBuilder.cs)
   - Find method: `BuildAsync()`
   - Reads entire file and builds multi-dimensional aggregation tree

3. Back to [src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs](src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs#L93)
   - Line 93: `await _treeRepository.SaveAsync(datasetId, root, schema, totalRows, ct)`
   - Saves computed tree to DB

4. Open [src/ChartEngine.Infrastructure/Persistence/Repositories/TreeRepository.cs](src/ChartEngine.Infrastructure/Persistence/Repositories/TreeRepository.cs)
   - Method: `SaveAsync()`
   - Serializes tree to JSON and stores in database

### Trace Path 5: From Pipeline to SignalR Notification

**Goal:** Follow how progress updates reach the client.

**Steps:**

1. [src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs](src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs#L86)
   - Line 86: `await PushProgressAsync(datasetId, 35, "Building aggregation tree", ct)`
   - Sends progress event

2. Search for method: `PushProgressAsync()` in same file
   - Calls `_hub.Clients.Group($"dataset-{datasetId}").SendAsync("ProgressUpdated", ...)`

3. Open [src/ChartEngine.API/Hubs/DatasetHub.cs](src/ChartEngine.API/Hubs/DatasetHub.cs)
   - Find hub class definition
   - Clients invoke methods here (e.g., `JoinGroup(groupName)`)

4. Client-side:
   - JavaScript/C# SignalR client receives `ProgressUpdated` event
   - Updates UI with progress bar

---

## 13. Complete Call Stack Diagram

```
POST /api/v1/datasets/upload
    ↓
DatasetsController.UploadAsync()
    ↓
    ├─→ Dataset.Create() [domain factory]
    ├─→ IFileStorage.SaveAsync() [blob storage]
    ├─→ IDatasetRepository.AddAsync() [EF Core INSERT]
    ├─→ DatasetProcessingChannel.TryEnqueue()
    └─→ return Accepted(DatasetUploadResult)

    ↓ [Async background task]

DatasetProcessingWorker (long-running loop)
    ↓
    DatasetProcessingChannel.ReadAllAsync() [blocks until item enqueued]
    ↓
    ProcessOneAsync()
        ↓
        IDatasetPipelineService.ProcessAsync()
            ├─→ Dataset.MarkAsProcessing()
            ├─→ IDatasetRepository.UpdateAsync() [EF Core UPDATE]
            │
            ├─→ ReadSampleRowsAsync()
            │
            ├─→ ISchemaDetector.Detect()
            │   └─→ analyzes column types
            │
            ├─→ PersistSchemaAsync()
            │   └─→ IDatasetRepository.SaveColumnsAsync()
            │       └─→ EF Core INSERT DatasetColumns
            │
            ├─→ ITreeBuilder.BuildAsync()
            │   └─→ reads entire file, computes aggregations
            │
            ├─→ ITreeRepository.SaveAsync()
            │   └─→ serializes tree, stores in DB
            │
            ├─→ Dataset.MarkAsReady()
            ├─→ IDatasetRepository.UpdateAsync() [EF Core UPDATE]
            │
            ├─→ PushProgressAsync() × N
            │   └─→ IHubContext<DatasetHub>.Clients.Group().SendAsync()
            │       └─→ SignalR event to client
            │
            └─→ catch exception → Dataset.MarkAsFailed()
```

---

## 14. Key Interfaces & Implementations Summary

| Interface                 | Implementation           | Purpose                                           |
| ------------------------- | ------------------------ | ------------------------------------------------- |
| `IDatasetService`         | `DatasetService`         | Coordinate upload, persist entity, enqueue job    |
| `IDatasetRepository`      | `DatasetRepository`      | Persist/query Dataset entities                    |
| `IFileStorage`            | `*FileStorage`           | Store uploaded file to blob                       |
| `ISchemaDetector`         | `SchemaDetector`         | Analyze sample rows, detect column types          |
| `ITreeBuilder`            | `TreeBuilder`            | Build multi-dim aggregation tree from entire file |
| `ITreeRepository`         | `TreeRepository`         | Persist computed tree structure                   |
| `IDatasetPipelineService` | `DatasetPipelineService` | Orchestrate: schema + tree + notify               |
| (Hub)                     | `DatasetHub`             | Broadcast progress events to clients              |

---

## 15. File Locations Summary

```
Entry Point:
  src/ChartEngine.API/Controllers/DatasetsController.cs        [HTTP endpoint]

Service Layer:
  src/ChartEngine.Infrastructure/Services/DatasetService.cs     [upload logic]
  src/ChartEngine.Infrastructure/Services/DatasetPipelineService.cs [orchestration]

Repositories:
  src/ChartEngine.Infrastructure/Persistence/Repositories/DatasetRepository.cs
  src/ChartEngine.Infrastructure/Persistence/Repositories/TreeRepository.cs

Domain:
  src/ChartEngine.Domain/Entities/Dataset.cs                    [entity]
  src/ChartEngine.Domain/Entities/AggregationTree.cs            [tree entity]

Analytics:
  src/ChartEngine.Infrastructure/Analytics/SchemaDetector.cs    [schema detection]
  src/ChartEngine.Infrastructure/Analytics/TreeBuilder.cs       [tree computation]

Background Jobs:
  src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingChannel.cs
  src/ChartEngine.Infrastructure/BackgroundJobs/DatasetProcessingWorker.cs

Real-time:
  src/ChartEngine.API/Hubs/DatasetHub.cs
  src/ChartEngine.API/Hubs/DatasetHubEvents.cs

DTOs:
  src/ChartEngine.Application/DTOs/DatasetUploadResult.cs
  src/ChartEngine.Application/DTOs/DatasetStatusDto.cs
  src/ChartEngine.Application/DTOs/DatasetSchemaDto.cs

Middleware:
  src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs
```

---

## 16. Testing Examples

### File: [tests/ChartEngine.Tests/SchemaDetectorTests.cs](tests/ChartEngine.Tests/SchemaDetectorTests.cs)

Shows example usage of `ISchemaDetector`:

```csharp
var schemaDetector = new SchemaDetector();
var schema = schemaDetector.Detect(sampleRows, estimatedRowCount);

Assert.NotNull(schema.Dimensions);
Assert.NotNull(schema.Metrics);
```

### File: [tests/ChartEngine.Tests/TreeBuilderTests.cs](tests/ChartEngine.Tests/TreeBuilderTests.cs)

Shows example usage of `ITreeBuilder`:

```csharp
var (root, rowCount) = await treeBuilder.BuildAsync(
    storagePath: "path/to/file.csv",
    schema: schema,
    onProgress: percent => { /* ... */ },
    ct: default
);

Assert.NotNull(root);
Assert.True(rowCount > 0);
```

---

## Summary

The **Dataset Upload & Ingestion** feature:

1. Accepts an HTTP multipart file upload
2. Immediately returns a dataset ID (HTTP 202)
3. Persists the file to blob storage and DB
4. Enqueues the dataset ID to a background processing channel
5. Background worker continuously reads from channel
6. Pipeline service orchestrates: schema detection + tree building
7. Results (schema, tree, row count) are persisted
8. Real-time SignalR events keep client updated (progress %)
9. On completion or error, client receives final status via SignalR
10. If error, exception details are captured in DB

---

**Next Steps:**

- Read the analytics/querying feature deep-dive (using the schema + tree)
- Read the export pipeline deep-dive (exporting subsets or aggregations)
- Set up local development environment and run tests from [tests/ChartEngine.Tests](tests/ChartEngine.Tests)
