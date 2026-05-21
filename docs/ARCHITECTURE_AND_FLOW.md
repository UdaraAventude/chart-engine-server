# Architecture & Request Flow

This document describes the main components and the typical end-to-end flows inside Chart Engine.

High-level components

- API Layer: `src/ChartEngine.API` — controllers, middleware, SignalR hubs.
- Application Layer: `src/ChartEngine.Application` — DTOs, service interfaces, hub contracts.
- Domain Layer: `src/ChartEngine.Domain` — entities and business rules.
- Infrastructure Layer: `src/ChartEngine.Infrastructure` — concrete services, persistence, storage, background jobs.

Typical request flow (example: dataset upload)

1. Client sends a POST to the API endpoint in [src/ChartEngine.API/Controllers/DatasetsController.cs](src/ChartEngine.API/Controllers/DatasetsController.cs).
2. Controller maps request data to an application DTO and calls an application service (interface in `ChartEngine.Application`).
3. The application service encapsulates orchestration logic and invokes domain operations and infrastructure services (persistence, storage, schema detection).
4. Long-running processing (schema detection, indexing) is delegated to background jobs in `ChartEngine.Infrastructure/BackgroundJobs`.
5. When processing completes, the infrastructure code uses SignalR hubs (`src/ChartEngine.API/Hubs`) to notify connected clients of status updates.

Sequence diagram (conceptual)

- Client → Controller → Application Service → Repository/Storage
- Application Service → Background Job → Infrastructure Processor → Hub → Client

Error handling

- Global exception handling is performed in [src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs](src/ChartEngine.API/Middleware/GlobalExceptionMiddleware.cs) which turns exceptions into HTTP responses and logs them.

Persistence & storage

- Persistence likely uses EF Core or a database provider in `ChartEngine.Infrastructure/Persistence`.
- Blob or object storage is implemented in `ChartEngine.Infrastructure/Storage` for uploaded files and export artifacts.

Testing

- Unit tests live in `tests/ChartEngine.Tests` and focus on application and domain services.
