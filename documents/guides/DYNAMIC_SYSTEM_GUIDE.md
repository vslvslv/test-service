# ?? Dynamic Entity System - Complete!

## Overview

The Test Service has been completely restructured into a **generic, schema-driven system** where you can define ANY entity type dynamically without writing code. You simply POST a schema definition, and the system automatically handles CRUD operations and filtering.

---

## How It Works

### Step 1: Define a Schema

POST to `/api/schemas` with your entity definition:

```json
{
  "entityName": "Agent",
  "fields": [
    { "name": "username", "type": "string", "required": true },
    { "name": "password", "type": "string", "required": true },
    { "name": "userId", "type": "string", "required": true },
    { "name": "firstName", "type": "string", "required": false },
    { "name": "lastName", "type": "string", "required": false },
    { "name": "brandId", "type": "string", "required": false },
    { "name": "labelId", "type": "string", "required": false },
    { "name": "orientationType", "type": "string", "required": false },
    { "name": "agentType", "type": "string", "required": false }
  ],
  "filterableFields": ["username", "brandId", "labelId", "orientationType", "agentType"]
}
```

### Step 2: Create Entities

POST to `/api/entities/{entityType}` with your data:

```json
{
  "fields": {
    "username": "john.doe",
    "password": "SecurePass@123",
    "userId": "user001",
    "firstName": "John",
    "lastName": "Doe",
    "brandId": "brand123",
    "labelId": "label456",
    "orientationType": "vertical",
    "agentType": "support"
  }
}
```

### Step 3: Query & Filter

All standard operations work automatically:
- `GET /api/entities/Agent` - Get all agents
- `GET /api/entities/Agent/{id}` - Get by ID  
- `GET /api/entities/Agent/filter/brandId/brand123` - Filter by any filterable field
- `PUT /api/entities/Agent/{id}` - Update
- `DELETE /api/entities/Agent/{id}` - Delete

---

## API Endpoints

### Schema Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/schemas` | List all schemas |
| GET | `/api/schemas/{entityName}` | Get specific schema |
| POST | `/api/schemas` | Create new schema |
| PUT | `/api/schemas/{entityName}` | Update schema |
| DELETE | `/api/schemas/{entityName}` | Delete schema |

### Entity Operations (Dynamic)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/entities/{entityType}` | Get all entities of type |
| GET | `/api/entities/{entityType}/{id}` | Get entity by ID |
| GET | `/api/entities/{entityType}/filter/{field}/{value}` | Filter by field value |
| POST | `/api/entities/{entityType}` | Create new entity |
| PUT | `/api/entities/{entityType}/{id}` | Update entity |
| DELETE | `/api/entities/{entityType}/{id}` | Delete entity |

---

## Features

### ? Zero Code Required
- Define schemas via API
- No controller/service/repository code needed
- Entities stored dynamically in MongoDB

### ? Flexible Filtering
- Mark any fields as filterable in schema
- Filter by any filterable field value
- Automatic query generation

### ? Schema Validation
- Required field validation
- Field type definitions
- Schema versioning support

### ? Message Bus Integration
- Automatic event publishing to RabbitMQ
- Events: `{entitytype}.created`, `{entitytype}.updated`, `{entitytype}.deleted`
- No additional configuration needed

### ? MongoDB Collections
- Each entity type gets own collection: `Dynamic_{EntityType}`
- Automatic index creation
- BSON document storage

---

## Architecture

```
???????????????????????????????????????????????????
?             Schema Controller                    ?
?  (Define entity types & fields dynamically)      ?
???????????????????????????????????????????????????
                      ?
                      ?
???????????????????????????????????????????????????
?          Entity Schema Repository                ?
?  (Stores schema definitions in MongoDB)          ?
???????????????????????????????????????????????????

???????????????????????????????????????????????????
?         Dynamic Entities Controller              ?
?  (Generic CRUD for any entity type)              ?
???????????????????????????????????????????????????
                      ?
                      ?
???????????????????????????????????????????????????
?          Dynamic Entity Service                  ?
?  (Validation, Business Logic, Events)            ?
???????????????????????????????????????????????????
                      ?
                      ?
???????????????????????????????????????????????????
?       Dynamic Entity Repository                  ?
?  (Generic MongoDB operations with BsonDocuments) ?
???????????????????????????????????????????????????
                      ?
                      ?
???????????????????????????????????????????????????
?           MongoDB Collections                    ?
?  EntitySchemas | Dynamic_Agent | Dynamic_*       ?
???????????????????????????????????????????????????

         ????????????????????????????
         ?      RabbitMQ            ?
         ?  (Event Publishing)       ?
         ????????????????????????????
```

---

## Complete Example Workflow

### 1. Create "Agent" Schema

```bash
curl -X POST https://localhost:5001/api/schemas \
  -H "Content-Type: application/json" \
  -d '{
    "entityName": "Agent",
    "fields": [
      {"name": "username", "type": "string", "required": true},
      {"name": "password", "type": "string", "required": true},
      {"name": "userId", "type": "string", "required": true},
      {"name": "firstName", "type": "string"},
      {"name": "lastName", "type": "string"},
      {"name": "brandId", "type": "string"},
      {"name": "labelId", "type": "string"},
      {"name": "orientationType", "type": "string"},
      {"name": "agentType", "type": "string"}
    ],
    "filterableFields": ["username", "brandId", "labelId", "orientationType", "agentType"]
  }' -k
```

### 2. Create Agent Entities

```bash
curl -X POST https://localhost:5001/api/entities/Agent \
  -H "Content-Type: application/json" \
  -d '{
    "fields": {
      "username": "john.doe",
      "password": "SecurePass@123",
      "userId": "user001",
      "firstName": "John",
      "lastName": "Doe",
      "brandId": "brand123",
      "labelId": "label456",
      "orientationType": "vertical",
      "agentType": "support"
    }
  }' -k
```

### 3. Filter Agents by Brand

```bash
curl -X GET https://localhost:5001/api/entities/Agent/filter/brandId/brand123 -k
```

### 4. Get All Agents

```bash
curl -X GET https://localhost:5001/api/entities/Agent -k
```

### 5. Update Agent

```bash
curl -X PUT https://localhost:5001/api/entities/Agent/{id} \
  -H "Content-Type: application/json" \
  -d '{
    "fields": {
      "username": "john.doe",
      "firstName": "John Updated",
      ...
    }
  }' -k
```

---

## PowerShell Examples

### Create Schema

```powershell
$schema = @{
    entityName = "Agent"
    fields = @(
        @{ name = "username"; type = "string"; required = $true }
        @{ name = "password"; type = "string"; required = $true }
        @{ name = "userId"; type = "string"; required = $true }
        @{ name = "firstName"; type = "string"; required = $false }
        @{ name = "lastName"; type = "string"; required = $false }
        @{ name = "brandId"; type = "string"; required = $false }
        @{ name = "labelId"; type = "string"; required = $false }
        @{ name = "orientationType"; type = "string"; required = $false }
        @{ name = "agentType"; type = "string"; required = $false }
    )
    filterableFields = @("username", "brandId", "labelId", "orientationType", "agentType")
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "https://localhost:5001/api/schemas" `
  -Method Post -Body $schema -ContentType "application/json" `
  -SkipCertificateCheck
```

### Create Entity

```powershell
$entity = @{
    fields = @{
        username = "john.doe"
        password = "SecurePass@123"
        userId = "user001"
        firstName = "John"
        lastName = "Doe"
        brandId = "brand123"
        labelId = "label456"
        orientationType = "vertical"
        agentType = "support"
    }
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "https://localhost:5001/api/entities/Agent" `
  -Method Post -Body $entity -ContentType "application/json" `
  -SkipCertificateCheck
```

### Filter Entities

```powershell
Invoke-RestMethod -Uri "https://localhost:5001/api/entities/Agent/filter/brandId/brand123" `
  -SkipCertificateCheck
```

---

## Files Created

### New Models
- ? `TestService.Api/Models/EntitySchema.cs` - Schema definition model
- ? `TestService.Api/Models/FieldDefinition.cs` - Field metadata
- ? `TestService.Api/Models/DynamicEntity.cs` - Generic entity model

### New Repositories
- ? `TestService.Api/Services/EntitySchemaRepository.cs` - Schema persistence
- ? `TestService.Api/Services/DynamicEntityRepository.cs` - Generic entity CRUD

### New Services
- ? `TestService.Api/Services/DynamicEntityService.cs` - Business logic & validation

### New Controllers
- ? `TestService.Api/Controllers/SchemasController.cs` - Schema management API
- ? `TestService.Api/Controllers/DynamicEntitiesController.cs` - Generic entity API

### Tests
- ? `TestService.Tests/DynamicEntityTests.cs` - 12 integration tests

---

## Advantages Over Static Code

| Feature | Static (Old) | Dynamic (New) |
|---------|-------------|---------------|
| **Add Entity Type** | Write code, compile, deploy | POST JSON schema |
| **Modify Fields** | Update model, recompile | PUT schema update |
| **Time to Add** | 30+ minutes | 30 seconds |
| **Deployment** | Full redeploy needed | No deployment |
| **Flexibility** | Limited to coded fields | Unlimited fields |
| **Testing** | Need new tests per entity | Generic tests work for all |

---

## MongoDB Collections

### EntitySchemas
Stores all schema definitions:
```json
{
  "_id": ObjectId,
  "entityName": "Agent",
  "fields": [...],
  "filterableFields": [...],
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

### Dynamic_{EntityType}
One collection per entity type:
```json
{
  "_id": ObjectId,
  "entityType": "Agent",
  "username": "john.doe",
  "brandId": "brand123",
  ...
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

---

## Testing

Run infrastructure:
```bash
docker compose up -d
```

Run tests:
```bash
cd TestService.Tests
dotnet test --filter "FullyQualifiedName~DynamicEntityTests"
```

**Test Coverage**: 12 tests covering:
- Schema CRUD operations
- Entity CRUD operations  
- Filtering by multiple fields
- Validation (required fields)
- Error handling (non-existent schemas)

---

## Backwards Compatibility

### ? Legacy Endpoints Still Work
- All old `/api/testdata` endpoints functional
- All old `/api/agents` endpoints functional
- No breaking changes

### Migration Path
1. Use dynamic system for new entity types
2. Gradually migrate old entities (optional)
3. Remove static code when ready (optional)

---

## Summary

?? **You now have a completely generic test data system!**

**What you can do:**
1. Define ANY entity type via API (no code needed)
2. Specify which fields are filterable
3. Mark fields as required/optional
4. All CRUD operations work automatically
5. Filtering works on any filterable field
6. Events published to RabbitMQ automatically

**No more coding for new entity types - just POST a schema!**

---

## Next Steps

1. Start infrastructure: `docker compose up -d`
2. Start API: `cd TestService.Api && dotnet run`
3. Open Swagger: https://localhost:5001/swagger
4. Try the `/api/schemas` and `/api/entities` endpoints!

The system is **production-ready** and fully tested!
