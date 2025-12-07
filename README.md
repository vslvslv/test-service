# Test Service

A microservice application for dynamic test data storage, consumption, aggregation, and manipulation using ASP.NET Core, MongoDB, and RabbitMQ.

## ?? Quick Start

```bash
# 1. Start infrastructure
docker compose up -d

# 2. Run the API
cd TestService.Api && dotnet run

# 3. Open Swagger UI
# Navigate to: https://localhost:5001/swagger
```

## ?? Documentation

**Complete documentation is available in the [`documents/`](documents/) folder.**

### Quick Links
- **[Documentation Index](documents/INDEX.md)** - Complete documentation navigation
- **[Dynamic System Guide](documents/guides/DYNAMIC_SYSTEM_GUIDE.md)** - Full guide to the dynamic entity system
- **[Parallel Test Execution](documents/guides/PARALLEL_TEST_EXECUTION_GUIDE.md)** - Thread-safe test data management
- **[Quick Reference](documents/guides/QUICK_REFERENCE.md)** - Common commands and operations
- **[API Reference](documents/api-reference/AGENT_API_DOCUMENTATION.md)** - Legacy Agent API documentation

---

## Architecture

### Dynamic Entity System

This service uses a **schema-driven dynamic entity system** where you can define ANY entity type via API without writing code!

**Example:**
```bash
# 1. Define a schema
POST /api/schemas
{
  "entityName": "Agent",
  "fields": [...],
  "filterableFields": ["brandId", "labelId"],
  "excludeOnFetch": true  // For parallel test execution
}

# 2. Create entities
POST /api/entities/Agent
{
  "fields": { "username": "john.doe", "brandId": "brand123" }
}

# 3. Query with automatic filtering
GET /api/entities/Agent/filter/brandId/brand123
```

### Components

1. **TestService.Api** - RESTful API service
   - Dynamic schema-based entity system
   - MongoDB document storage
   - RabbitMQ message bus integration
   - Automatic CRUD operations
   - Thread-safe parallel test execution support
   - Built with ASP.NET Core Web API

2. **TestService.Tests** - NUnit test project
   - Integration tests for all APIs
   - Dynamic entity tests
   - Parallel execution tests
   - Uses WebApplicationFactory

### Technology Stack

- **Framework**: .NET 10.0
- **Database**: MongoDB (Document-based NoSQL database)
- **Message Bus**: RabbitMQ (AMQP message broker)
- **Testing**: NUnit with Microsoft.AspNetCore.Mvc.Testing
- **API Documentation**: Swagger/OpenAPI

---

## Prerequisites

- .NET 10.0 SDK or later
- Docker (for MongoDB and RabbitMQ)

---

## Quick Start with Docker

```bash
# Start MongoDB and RabbitMQ
docker compose up -d

# Verify containers are running
docker ps
```

**Services:**
- MongoDB: `localhost:27017`
- RabbitMQ: `localhost:5672` (AMQP)
- RabbitMQ Management: `http://localhost:15672` (guest/guest)

---

## Running the Application

### Build the solution

```bash
dotnet build
```

### Run the API

```bash
cd TestService.Api
dotnet run
```

**API URLs:**
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`
- Swagger UI: `https://localhost:5001/swagger`

### Run the Tests

```bash
cd TestService.Tests
dotnet test
```

**Test Coverage**: 32 integration tests
- 12 Dynamic Entity tests
- 11 Agent API tests (legacy)
- 9 TestData API tests (legacy)

---

## Key Features

### ? Dynamic Entity System
- Define entity types via API (no code required)
- Automatic CRUD operations
- Flexible filtering on any field
- Schema validation
- MongoDB collections created automatically

### ?? Parallel Test Execution
- `excludeOnFetch` flag prevents test conflicts
- Thread-safe `/next` endpoint
- Atomic MongoDB operations
- Easy cleanup with `/reset-all`

### ?? Message Bus Integration
- Automatic event publishing to RabbitMQ
- Events: `{type}.created`, `{type}.updated`, `{type}.deleted`, `{type}.consumed`
- Topic-based routing

### ?? Multiple API Styles
- **Dynamic API**: Schema-driven entities (`/api/entities`, `/api/schemas`)
- **Legacy API**: Static Agent API (`/api/agents`)
- **Legacy API**: Static TestData API (`/api/testdata`)

---

## API Endpoints

### Dynamic Entity System (Recommended)

#### Schema Management
- `GET /api/schemas` - List all schemas
- `GET /api/schemas/{entityName}` - Get specific schema
- `POST /api/schemas` - Create new schema
- `PUT /api/schemas/{entityName}` - Update schema
- `DELETE /api/schemas/{entityName}` - Delete schema

#### Entity Operations
- `GET /api/entities/{type}` - Get all entities (excludes consumed if enabled)
- `GET /api/entities/{type}/{id}` - Get by ID (marks as consumed if enabled)
- `GET /api/entities/{type}/next` - Get next available (atomic, thread-safe)
- `GET /api/entities/{type}/filter/{field}/{value}` - Filter by field
- `POST /api/entities/{type}` - Create entity
- `PUT /api/entities/{type}/{id}` - Update entity
- `DELETE /api/entities/{type}/{id}` - Delete entity
- `POST /api/entities/{type}/{id}/reset` - Reset consumed flag
- `POST /api/entities/{type}/reset-all` - Reset all consumed entities

### Legacy APIs (Still Supported)

See [Agent API Documentation](documents/api-reference/AGENT_API_DOCUMENTATION.md) for legacy endpoint details.

---

## Example Usage

### Create a Schema and Entities

```bash
# 1. Create schema
curl -X POST https://localhost:5001/api/schemas \
  -H "Content-Type: application/json" \
  -d '{
    "entityName": "Agent",
    "fields": [
      {"name": "username", "type": "string", "required": true},
      {"name": "brandId", "type": "string"},
      {"name": "agentType", "type": "string"}
    ],
    "filterableFields": ["username", "brandId", "agentType"],
    "excludeOnFetch": true
  }' -k

# 2. Create entities
curl -X POST https://localhost:5001/api/entities/Agent \
  -H "Content-Type: application/json" \
  -d '{
    "fields": {
      "username": "john.doe",
      "brandId": "brand123",
      "agentType": "support"
    }
  }' -k

# 3. Get next available (for parallel tests)
curl -X GET https://localhost:5001/api/entities/Agent/next -k

# 4. Filter by brand
curl -X GET https://localhost:5001/api/entities/Agent/filter/brandId/brand123 -k
```

---

## Configuration

Configuration is in `appsettings.json`:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "TestServiceDb"
  },
  "RabbitMqSettings": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "ExchangeName": "test-service-exchange"
  }
}
```

---

## Project Structure

```
test-service/
??? TestService.Api/
?   ??? Configuration/
?   ??? Controllers/
?   ?   ??? DynamicEntitiesController.cs  (NEW)
?   ?   ??? SchemasController.cs          (NEW)
?   ?   ??? AgentsController.cs           (Legacy)
?   ?   ??? TestDataController.cs         (Legacy)
?   ??? Models/
?   ?   ??? EntitySchema.cs               (NEW)
?   ?   ??? DynamicEntity.cs              (NEW)
?   ?   ??? Agent.cs                      (Legacy)
?   ?   ??? TestData.cs                   (Legacy)
?   ??? Services/
?   ?   ??? EntitySchemaRepository.cs     (NEW)
?   ?   ??? DynamicEntityRepository.cs    (NEW)
?   ?   ??? DynamicEntityService.cs       (NEW)
?   ?   ??? MessageBusService.cs          (Shared)
?   ??? Program.cs
??? TestService.Tests/
?   ??? DynamicEntityTests.cs             (NEW)
?   ??? AgentApiTests.cs                  (Legacy)
?   ??? TestDataApiTests.cs               (Legacy)
??? documents/                            ??
?   ??? INDEX.md                          - Documentation index
?   ??? guides/                           - User guides
?   ??? api-reference/                    - API documentation
?   ??? architecture/                     - Architecture docs
?   ??? test-results/                     - Test results
??? docker-compose.yml
??? README.md (this file)
```

---

## Documentation

All documentation is organized in the [`documents/`](documents/) folder:

### ?? Guides
- [Dynamic System Guide](documents/guides/DYNAMIC_SYSTEM_GUIDE.md) - Complete guide to dynamic entities
- [Parallel Test Execution Guide](documents/guides/PARALLEL_TEST_EXECUTION_GUIDE.md) - Thread-safe testing
- [Verification Guide](documents/guides/VERIFICATION_GUIDE.md) - How to verify the system
- [Quick Reference](documents/guides/QUICK_REFERENCE.md) - Command cheat sheet

### ?? API Reference
- [Agent API Documentation](documents/api-reference/AGENT_API_DOCUMENTATION.md) - Legacy Agent API

### ??? Architecture
- [Restructuring Summary](documents/architecture/RESTRUCTURING_SUMMARY.md) - System evolution

### ?? Test Results
- [Test Execution Results](documents/test-results/TEST_EXECUTION_RESULTS.md)
- [Verification Results](documents/test-results/VERIFICATION_RESULTS.md)

**Start here:** [Documentation Index](documents/INDEX.md)

---

## Development

### Adding New Entity Types

**No code required!** Just POST a schema:

```bash
POST /api/schemas
{
  "entityName": "YourEntityName",
  "fields": [...],
  "filterableFields": [...],
  "excludeOnFetch": false
}
```

The system automatically:
- Creates MongoDB collection
- Enables CRUD operations
- Enables filtering
- Publishes RabbitMQ events

### Adding Code Features

1. Define models in `Models/`
2. Create repositories in `Services/`
3. Implement services in `Services/`
4. Add controllers in `Controllers/`
5. Write tests in `TestService.Tests/`

---

## Testing

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~DynamicEntityTests"

# Run with detailed output
dotnet test --verbosity normal
```

---

## Logging

Configure logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## License

This project is open source and available under the MIT License.

---

## Getting Help

1. Check the [Documentation Index](documents/INDEX.md)
2. Review the [Quick Reference](documents/guides/QUICK_REFERENCE.md)
3. See examples in the [guides](documents/guides/)
4. Use Swagger UI for interactive API exploration
5. Check test results for working examples

---

**Made with ?? for dynamic test data management**
