# Chart Engine — Project Overview

This repository implements Chart Engine, a backend service for ingesting, analyzing, and exporting tabular datasets. It is built with .NET (C#) and uses a layered architecture to separate API, application logic, domain models, and infrastructure concerns.

Key projects

- `src/ChartEngine.API` — ASP.NET Core HTTP API and SignalR hubs.
- `src/ChartEngine.Application` — Application layer: DTOs, hub interfaces, service interfaces.
- `src/ChartEngine.Domain` — Domain entities, value objects, enums, exceptions.
- `src/ChartEngine.Infrastructure` — Implementations: persistence, storage, background jobs, services.
- `tests/ChartEngine.Tests` — Unit tests for core services.

Primary responsibilities

- Dataset ingestion and schema detection
- Querying and analytics over datasets
- Exporting dataset slices or aggregated results
- Real-time notifications via SignalR hubs
- Background processing (jobs for import/export/processing)

Design principles

- Layered architecture (API → Application → Domain → Infrastructure)
- Clear separation of DTOs and domain objects
- Asynchronous/background processing for long-running tasks
- Real-time client updates using Hubs

How to use this documentation

- Read [Architecture & Flow](ARCHITECTURE_AND_FLOW.md) for end-to-end request flow.
- Read [Feature Implementation](FEATURE_IMPLEMENTATION.md) for feature-by-feature code pointers.
- Read [Tracing Guide](TRACING_GUIDE.md) for step-by-step instructions to locate and follow code for any feature.
