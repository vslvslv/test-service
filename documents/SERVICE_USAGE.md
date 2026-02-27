# Service Usage Guide

This document explains how to use the Test Service API and Web UI for creating and managing dynamic test data.

## Overview

The Test Service is a schema-driven data service that lets you define entity types at runtime, then create, query, and manage entities without code changes. It includes:

- REST API (TestService.Api)
- Swagger/OpenAPI docs
- Web UI (testservice-web)
- MongoDB for storage
- RabbitMQ for messaging

## How to Run

### Docker (recommended)

Start all services:

```bash
docker compose -f infrastructure/docker-compose.yml up -d
```

### Local development

```bash
# Start infrastructure
docker compose -f infrastructure/docker-compose.yml up mongodb rabbitmq -d

# Run API
cd TestService.Api && dotnet run

# Run Web UI
cd testservice-web && npm install && npm run dev
```

## Access Points

- API (HTTP): http://localhost:5000
- API (HTTPS): https://localhost:5001
- Swagger UI: https://localhost:5001/swagger
- Web UI (dev): http://localhost:5173
- Web UI (docker): http://localhost:3000
- RabbitMQ Management: http://localhost:15672 (guest/guest)

## Core API Workflow

### 1) Define a schema

Schemas describe a dynamic entity type and its fields. Create a schema by sending a POST to `/api/schemas`.

Example:

```json
{
  "entityName": "Agent",
  "fields": [
    { "name": "username", "type": "string", "required": true },
    { "name": "brandId", "type": "string", "required": false }
  ],
  "filterableFields": ["brandId"],
  "excludeOnFetch": true
}
```

Notes:
- `filterableFields` enables query endpoints for those fields.
- `excludeOnFetch` supports parallel test execution by preventing global fetches from reusing data.

### 2) Create an entity

Once the schema exists, create entities with `POST /api/entities/{entityName}`.

Example:

```json
{
  "fields": {
    "username": "john.doe",
    "brandId": "brand123"
  }
}
```

### 3) Query entities

Use standard collection endpoints (see Swagger) or filter endpoints:

```
GET /api/entities/Agent/filter/brandId/brand123
```

### 4) Update and delete

Update and delete endpoints are available for dynamic entities. Refer to Swagger for the exact routes and payload shapes.

## Web UI Usage

The Web UI provides a graphical interface to:

- Browse schemas
- Create and manage entities
- Run common test data workflows

If running locally in dev mode, open http://localhost:5173. If running via Docker, use http://localhost:3000.

## Testing

Integration tests live in `TestService.Tests`. Run:

```bash
cd TestService.Tests && dotnet test
```

## Tips

- Always create a schema before creating entities of that type.
- Use Swagger to explore all API endpoints and payloads.
- For parallel test execution, ensure `excludeOnFetch` is set in the schema to avoid accidental reuse of data.

## Related Docs

- Documentation index: documents/INDEX.md
- Dynamic system guide: documents/guides/DYNAMIC_SYSTEM_GUIDE.md
- Quick reference: documents/guides/QUICK_REFERENCE.md
