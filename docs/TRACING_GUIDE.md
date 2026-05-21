# Code Tracing Guide — How to follow a feature to its implementation

This guide gives a reproducible approach to trace any feature from an external request down to data persistence and background workers.

General tracing steps

1. Identify the public HTTP or SignalR entry point (Controller or Hub).
2. Note the method name and the DTOs used.
3. Locate the application service interface (in `src/ChartEngine.Application/Interfaces` or `Services`) called by the controller.
4. Find the concrete implementation in `src/ChartEngine.Infrastructure` (Services folder).
5. Inspect domain entities in `src/ChartEngine.Domain/Entities` used by the implementation.
6. Check background jobs in `src/ChartEngine.Infrastructure/BackgroundJobs` for long-running processing.
7. Check `src/ChartEngine.API/Middleware` for cross-cutting concerns like error handling and logging.

Example: tracing a dataset upload end-to-end

- Start: [src/ChartEngine.API/Controllers/DatasetsController.cs](src/ChartEngine.API/Controllers/DatasetsController.cs)
  - Find the upload endpoint (POST). Note the DTO (upload request/response types).
- Application: find service interface in `src/ChartEngine.Application/Interfaces` or `src/ChartEngine.Application/Services` that the controller calls (e.g., `IDatasetService`).
- Infrastructure: find `IDatasetService` implementation in `src/ChartEngine.Infrastructure/Services` (name pattern: `DatasetService`, `DatasetUploadService`).
  - Look for methods that enqueue jobs, call persistence layers, or write to storage.
- Background job: open `src/ChartEngine.Infrastructure/BackgroundJobs` for the worker that performs schema detection and indexing.
  - Inspect how the job updates the `Dataset` entity and persists changes.
- Notifications: open `src/ChartEngine.API/Hubs` to see how the Hub publishes status updates (group names in `ChartEngine.Application/Constants`).

Practical commands (from repo root)
Use the repo search features in your editor or these command-line examples:

```powershell
# find references to Dataset upload
rg "DatasetsController" -n
rg "Dataset" src -n

# find where interface is implemented
rg "interface IDataset" -n
rg "DatasetService" -n
```

Tips

- Start at controllers because they are the single point for external behavior.
- Use DTOs names as anchors to find service interfaces and implementations.
- Unit tests in `tests/ChartEngine.Tests` are good examples demonstrating how application services are used and expected results.
