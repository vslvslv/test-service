# Agent API Documentation

## Overview

The Agent API provides CRUD operations and filtering capabilities for managing test Agent objects. Agents can be created, retrieved, updated, and deleted through RESTful endpoints.

## Agent Model

```json
{
  "id": "string (MongoDB ObjectId)",
  "username": "string",
  "password": "string",
  "userId": "string",
  "firstName": "string",
  "lastName": "string",
  "brandId": "string",
  "labelId": "string",
  "orientationType": "string",
  "agentType": "string",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

## API Endpoints

### Base URL
```
/api/agents
```

---

## 1. Create Agent

**POST** `/api/agents`

Creates a new agent in the system.

### Request Body
```json
{
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
```

### Response
**Status:** 201 Created
```json
{
  "id": "507f1f77bcf86cd799439011",
  "username": "john.doe",
  "password": "SecurePass@123",
  "userId": "user001",
  "firstName": "John",
  "lastName": "Doe",
  "brandId": "brand123",
  "labelId": "label456",
  "orientationType": "vertical",
  "agentType": "support",
  "createdAt": "2025-01-06T10:30:00Z",
  "updatedAt": "2025-01-06T10:30:00Z"
}
```

### Example cURL
```bash
curl -X POST https://localhost:5001/api/agents \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john.doe",
    "password": "SecurePass@123",
    "userId": "user001",
    "firstName": "John",
    "lastName": "Doe",
    "brandId": "brand123",
    "labelId": "label456",
    "orientationType": "vertical",
    "agentType": "support"
  }' -k
```

---

## 2. Get All Agents

**GET** `/api/agents`

Retrieves all agents from the system.

### Response
**Status:** 200 OK
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "john.doe",
    "firstName": "John",
    "lastName": "Doe",
    ...
  },
  {
    "id": "507f1f77bcf86cd799439012",
    "username": "jane.smith",
    "firstName": "Jane",
    "lastName": "Smith",
    ...
  }
]
```

---

## 3. Get Agent by ID

**GET** `/api/agents/{id}`

Retrieves a specific agent by their MongoDB ObjectId.

### Parameters
- `id` (path) - The MongoDB ObjectId of the agent

### Response
**Status:** 200 OK
```json
{
  "id": "507f1f77bcf86cd799439011",
  "username": "john.doe",
  ...
}
```

**Status:** 404 Not Found (if agent doesn't exist)

---

## 4. Get Agent by Username

**GET** `/api/agents/username/{username}`

Retrieves a specific agent by their username.

### Parameters
- `username` (path) - The username of the agent

### Response
**Status:** 200 OK
```json
{
  "id": "507f1f77bcf86cd799439011",
  "username": "john.doe",
  ...
}
```

**Status:** 404 Not Found (if agent doesn't exist)

### Example
```bash
curl -X GET https://localhost:5001/api/agents/username/john.doe -k
```

---

## 5. Get Agents by Brand ID

**GET** `/api/agents/brand/{brandId}`

Retrieves all agents associated with a specific brand.

### Parameters
- `brandId` (path) - The brand identifier

### Response
**Status:** 200 OK
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "john.doe",
    "brandId": "brand123",
    ...
  },
  {
    "id": "507f1f77bcf86cd799439012",
    "username": "jane.smith",
    "brandId": "brand123",
    ...
  }
]
```

### Example
```bash
curl -X GET https://localhost:5001/api/agents/brand/brand123 -k
```

---

## 6. Get Agents by Label ID

**GET** `/api/agents/label/{labelId}`

Retrieves all agents associated with a specific label.

### Parameters
- `labelId` (path) - The label identifier

### Response
**Status:** 200 OK
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "john.doe",
    "labelId": "label456",
    ...
  }
]
```

---

## 7. Get Agents by Orientation Type

**GET** `/api/agents/orientation/{orientationType}`

Retrieves all agents with a specific orientation type.

### Parameters
- `orientationType` (path) - The orientation type (e.g., "vertical", "horizontal")

### Response
**Status:** 200 OK
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "john.doe",
    "orientationType": "vertical",
    ...
  }
]
```

---

## 8. Get Agents by Agent Type

**GET** `/api/agents/type/{agentType}`

Retrieves all agents of a specific type.

### Parameters
- `agentType` (path) - The agent type (e.g., "support", "sales", "technical")

### Response
**Status:** 200 OK
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "john.doe",
    "agentType": "support",
    ...
  }
]
```

---

## 9. Update Agent

**PUT** `/api/agents/{id}`

Updates an existing agent.

### Parameters
- `id` (path) - The MongoDB ObjectId of the agent

### Request Body
```json
{
  "username": "john.doe",
  "password": "NewSecurePass@123",
  "userId": "user001",
  "firstName": "John",
  "lastName": "Doe Updated",
  "brandId": "brand123",
  "labelId": "label456",
  "orientationType": "horizontal",
  "agentType": "technical"
}
```

### Response
**Status:** 204 No Content

**Status:** 404 Not Found (if agent doesn't exist)

### Example
```bash
curl -X PUT https://localhost:5001/api/agents/507f1f77bcf86cd799439011 \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john.doe",
    "firstName": "John",
    "lastName": "Doe Updated",
    ...
  }' -k
```

---

## 10. Delete Agent

**DELETE** `/api/agents/{id}`

Deletes an agent from the system.

### Parameters
- `id` (path) - The MongoDB ObjectId of the agent

### Response
**Status:** 204 No Content

**Status:** 404 Not Found (if agent doesn't exist)

### Example
```bash
curl -X DELETE https://localhost:5001/api/agents/507f1f77bcf86cd799439011 -k
```

---

## Message Bus Events

The Agent API publishes events to RabbitMQ for all create, update, and delete operations:

### Events Published
- `agent.created` - When a new agent is created
- `agent.updated` - When an agent is updated
- `agent.deleted` - When an agent is deleted

### Event Format
```json
{
  "id": "507f1f77bcf86cd799439011",
  "username": "john.doe",
  "firstName": "John",
  ...
}
```

---

## Filtering Examples

### Filter by multiple criteria programmatically

```bash
# Get all support agents
curl -X GET https://localhost:5001/api/agents/type/support -k

# Get all agents in a specific brand
curl -X GET https://localhost:5001/api/agents/brand/brand123 -k

# Get all agents with vertical orientation
curl -X GET https://localhost:5001/api/agents/orientation/vertical -k
```

---

## PowerShell Examples

### Create Agent
```powershell
$agent = @{
    username = "john.doe"
    password = "SecurePass@123"
    userId = "user001"
    firstName = "John"
    lastName = "Doe"
    brandId = "brand123"
    labelId = "label456"
    orientationType = "vertical"
    agentType = "support"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:5001/api/agents" `
  -Method Post -Body $agent -ContentType "application/json" `
  -SkipCertificateCheck
```

### Get by Username
```powershell
Invoke-RestMethod -Uri "https://localhost:5001/api/agents/username/john.doe" `
  -SkipCertificateCheck
```

### Filter by Brand
```powershell
Invoke-RestMethod -Uri "https://localhost:5001/api/agents/brand/brand123" `
  -SkipCertificateCheck
```

---

## Error Handling

### Error Responses

**400 Bad Request**
```json
{
  "errors": {
    "Username": ["The Username field is required."]
  }
}
```

**404 Not Found**
```json
{
  "message": "Agent not found"
}
```

**500 Internal Server Error**
```json
{
  "message": "Internal server error"
}
```

---

## Database Collection

Agents are stored in MongoDB:
- **Database:** TestServiceDb
- **Collection:** Agents

### Indexes Recommended
For optimal query performance, consider adding indexes on:
- `username` (unique)
- `brandId`
- `labelId`
- `agentType`
- `orientationType`

---

## Testing

All endpoints are covered by integration tests. To run tests:

```bash
cd TestService.Tests
dotnet test --filter "FullyQualifiedName~AgentApiTests"
```

**Test Coverage:** 11 integration tests covering all CRUD operations and filtering scenarios.
