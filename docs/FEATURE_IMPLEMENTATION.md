# Features and Implementation Details

This file documents the main features and points to the code responsible for each feature so you can examine and extend the implementation.

1. Dataset management (upload, schema detection, status)

- API entry points: [src/ChartEngine.API/Controllers/DatasetsController.cs](src/ChartEngine.API/Controllers/DatasetsController.cs)
- DTOs: `src/ChartEngine.Application/DTOs` (e.g., `DatasetUploadResult.cs`, `DatasetSchemaDto.cs`)
- Domain entity: `src/ChartEngine.Domain/Entities/Dataset.cs`
- Background processing: `src/ChartEngine.Infrastructure/BackgroundJobs` (jobs that process uploads)
- Storage: `src/ChartEngine.Infrastructure/Storage` (file persist/stream)

How it's typically implemented

- Controller validates input and returns an initial `DatasetUploadResult` DTO.
- The Application service enqueues a background job to process and detect schema.
- Background job updates the `Dataset` entity, persists results, and notifies clients via Hub.

2. Analytics / Querying

- Controller: [src/ChartEngine.API/Controllers/AnalyticsController.cs](src/ChartEngine.API/Controllers/AnalyticsController.cs)
- Services: Look in `src/ChartEngine.Application/Services` and `src/ChartEngine.Infrastructure/Analytics` for implementations
- DTOs: `PagedListDto`, `PagedRowsDto` in `src/ChartEngine.Application/DTOs`

3. Export (jobs and files)

- Controller: [src/ChartEngine.API/Controllers/ExportController.cs](src/ChartEngine.API/Controllers/ExportController.cs)
- Domain: `ExportJob.cs` in `src/ChartEngine.Domain/Entities`
- Background export worker: `src/ChartEngine.Infrastructure/BackgroundJobs` (creates file, stores it, marks job status)

4. Real-time updates (SignalR)

- Hubs: `src/ChartEngine.API/Hubs` (e.g., `DatasetHub.cs`, `DatasetHubMarker.cs`)
- Application exposes hub group names in `ChartEngine.Application/Constants` and `ChartEngine.Application/Hubs` so infrastructure and controllers can publish updates.

5. Error handling & middleware

- See [src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs](src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs)

6. Tests

- Unit tests in `tests/ChartEngine.Tests` show example usage of services and expected behaviors for core algorithms like schema detection and tree building.

Next steps to inspect a feature's code

- Open the controller listed above, identify the public method used by clients, then follow interface calls to the Application layer and implementations in Infrastructure.
- Use repository-wide search for class names (e.g., `Dataset`, `ExportJob`, `DatasetHub`) to find all usages and implementations.
