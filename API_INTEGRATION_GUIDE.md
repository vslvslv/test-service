# Test Service - API Integration Guide

**Version:** 1.0.0  
**Base URL:** `http://localhost:5000` (Development) | `https://your-domain.com` (Production)  
**Authentication:** JWT Bearer Token  
**Date:** December 16, 2025

---

## Table of Contents

1. [Authentication](#1-authentication)
2. [Entity Schemas](#2-entity-schemas)
3. [Dynamic Entities](#3-dynamic-entities)
4. [Environments](#4-environments)
5. [Settings & API Keys](#5-settings--api-keys)
6. [Users](#6-users)
7. [Data Models](#7-data-models)
8. [Error Handling](#8-error-handling)
9. [Integration Examples](#9-integration-examples)

---

## 1. Authentication

### 1.1 Login
**Endpoint:** `POST /api/auth/login`  
**Authorization:** None (Public)  
**Description:** Authenticate user and receive JWT token

#### Request Body:
```json
{
  "username": "admin",
  "password": "Admin@123"
}
```

#### Response (200 OK):
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "507f1f77bcf86cd799439011",
    "username": "admin",
    "email": "admin@example.com",
    "role": "Admin",
    "createdAt": "2025-12-16T10:00:00Z"
  }
}
```

#### Default Credentials:
- Username: `admin`
- Password: `Admin@123`

### 1.2 Get Current User
**Endpoint:** `GET /api/auth/me`  
**Authorization:** Bearer Token Required  
**Description:** Get authenticated user information

#### Response (200 OK):
```json
{
  "id": "507f1f77bcf86cd799439011",
  "username": "admin",
  "email": "admin@example.com",
  "role": "Admin",
  "createdAt": "2025-12-16T10:00:00Z"
}
```

---

## 2. Entity Schemas

Entity schemas define the structure of dynamic entity types. Think of them as "tables" in a traditional database.

### 2.1 Get All Schemas
**Endpoint:** `GET /api/schemas`  
**Authorization:** Bearer Token Required

#### Response (200 OK):
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "entityName": "test-agent",
    "fields": [
      {
        "name": "username",
        "type": "string",
        "required": true,
        "isUnique": true,
        "description": "User's login name"
      },
      {
        "name": "userId",
        "type": "number",
        "required": true,
        "isUnique": true,
        "description": null
      },
      {
        "name": "brandId",
        "type": "number",
        "required": false,
        "isUnique": false,
        "description": null
      }
    ],
    "filterableFields": ["username", "brandId"],
    "uniqueFields": [],
    "useCompoundUnique": false,
    "excludeOnFetch": false,
    "createdAt": "2025-12-16T10:00:00Z",
    "updatedAt": "2025-12-16T10:00:00Z"
  }
]
```

### 2.2 Get Schema by Name
**Endpoint:** `GET /api/schemas/{entityName}`  
**Authorization:** Bearer Token Required

**Example:** `GET /api/schemas/test-agent`

#### Response (200 OK):
Same structure as single schema above.

### 2.3 Create Schema
**Endpoint:** `POST /api/schemas`  
**Authorization:** Bearer Token Required  
**Description:** Create a new entity schema (defines entity structure)

#### Request Body:
```json
{
  "entityName": "test-agent",
  "fields": [
    {
      "name": "username",
      "type": "string",
      "required": true,
      "isUnique": true,
      "description": "User's login name"
    },
    {
      "name": "userId",
      "type": "number",
      "required": true,
      "isUnique": true
    },
    {
      "name": "brandId",
      "type": "number",
      "required": false,
      "isUnique": false
    },
    {
      "name": "password",
      "type": "string",
      "required": true,
      "isUnique": false
    }
  ],
  "filterableFields": ["username", "brandId"],
  "excludeOnFetch": false
}
```

#### Field Types:
- `string` - Text values
- `number` - Numeric values (integer or decimal)
- `boolean` - True/False values
- `date` - Date/DateTime values
- `array` - List of values
- `object` - Nested JSON objects

#### Field Properties:
- `name` (string, required): Field name (alphanumeric + underscore)
- `type` (string, required): Data type
- `required` (boolean): If true, field must be provided when creating entities
- `isUnique` (boolean): If true, no two entities can have the same value for this field
- `description` (string, optional): Field description

#### Schema Properties:
- `entityName` (string, required): Name of the entity type (e.g., "test-agent")
- `fields` (array, required): List of field definitions
- `filterableFields` (array, optional): Fields that can be used in filter queries
- `uniqueFields` (array, optional): For compound unique constraints
- `useCompoundUnique` (boolean): If true, uniqueFields are checked together as a compound key
- `excludeOnFetch` (boolean): If true, entities are marked as "consumed" when fetched (useful for parallel test execution)

#### Response (201 Created):
Returns the created schema with ID and timestamps.

### 2.4 Update Schema
**Endpoint:** `PUT /api/schemas/{entityName}`  
**Authorization:** Bearer Token Required

#### Request Body:
Same as create, but for updating existing schema.

### 2.5 Delete Schema
**Endpoint:** `DELETE /api/schemas/{entityName}`  
**Authorization:** Bearer Token Required

#### Response (204 No Content):
Schema deleted successfully.

---

## 3. Dynamic Entities

Dynamic entities are instances of the schemas you've defined. Each entity conforms to its schema's structure.

### 3.1 Get All Entities by Type
**Endpoint:** `GET /api/entities/{entityType}?environment={env}`  
**Authorization:** Bearer Token Required  
**Description:** Get all entities of a specific type

**Example:** `GET /api/entities/test-agent?environment=qa`

#### Query Parameters:
- `environment` (optional): Filter by environment (dev, staging, qa, prod)

#### Response (200 OK):
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "entityType": "test-agent",
    "environment": "qa",
    "fields": {
      "username": "AutomationMgr_213",
      "userId": 12283351,
      "brandId": 14,
      "password": "welcome123"
    },
    "isConsumed": false,
    "createdAt": "2025-12-16T10:00:00Z",
    "updatedAt": "2025-12-16T10:00:00Z"
  }
]
```

### 3.2 Get Entity by ID
**Endpoint:** `GET /api/entities/{entityType}/{id}`  
**Authorization:** Bearer Token Required

**Example:** `GET /api/entities/test-agent/507f1f77bcf86cd799439011`

#### Response (200 OK):
Returns single entity object.

**Note:** If schema has `excludeOnFetch: true`, this endpoint will mark the entity as consumed.

### 3.3 Get Next Available Entity (Atomic)
**Endpoint:** `GET /api/entities/{entityType}/next?environment={env}`  
**Authorization:** Bearer Token Required  
**Description:** Atomically fetch and mark an entity as consumed (for parallel test execution)

**Example:** `GET /api/entities/test-agent/next?environment=qa`

#### Use Case:
Perfect for parallel test execution where multiple tests need unique test data simultaneously. This endpoint:
1. Finds a non-consumed entity
2. Marks it as consumed
3. Returns it
All in a single atomic operation to prevent race conditions.

#### Requirements:
- Schema must have `excludeOnFetch: true`

#### Response (200 OK):
Returns single entity object.

#### Response (404 Not Found):
```json
{
  "error": "No available entities found"
}
```

### 3.4 Filter Entities by Field Value
**Endpoint:** `GET /api/entities/{entityType}/filter/{fieldName}/{value}?environment={env}`  
**Authorization:** Bearer Token Required

**Example:** `GET /api/entities/test-agent/filter/brandId/14?environment=qa`

#### Requirements:
- Field must be in schema's `filterableFields` array

#### Response (200 OK):
Returns array of matching entities.

### 3.5 Create Entity
**Endpoint:** `POST /api/entities/{entityType}`  
**Authorization:** Bearer Token Required  
**Description:** Create a new entity instance

**Example:** `POST /api/entities/test-agent`

#### Request Body:
```json
{
  "fields": {
    "username": "AutomationMgr_213",
    "userId": 12283351,
    "brandId": 14,
    "password": "welcome123"
  },
  "environment": "qa"
}
```

#### Validation Rules:
- All `required` fields must be provided
- Fields marked as `isUnique` cannot have duplicate values
- Field values must match their defined types
- Unknown fields (not in schema) are ignored

#### Response (201 Created):
```json
{
  "id": "507f1f77bcf86cd799439011",
  "entityType": "test-agent",
  "environment": "qa",
  "fields": {
    "username": "AutomationMgr_213",
    "userId": 12283351,
    "brandId": 14,
    "password": "welcome123"
  },
  "isConsumed": false,
  "createdAt": "2025-12-16T10:00:00Z",
  "updatedAt": "2025-12-16T10:00:00Z"
}
```

#### Error (400 Bad Request):
```json
{
  "error": "Entity does not match schema for type: test-agent"
}
```

### 3.6 Update Entity
**Endpoint:** `PUT /api/entities/{entityType}/{id}`  
**Authorization:** Bearer Token Required

**Example:** `PUT /api/entities/test-agent/507f1f77bcf86cd799439011`

#### Request Body:
```json
{
  "fields": {
    "username": "UpdatedUsername",
    "userId": 12283351,
    "brandId": 15,
    "password": "newpassword123"
  },
  "environment": "qa"
}
```

#### Response (204 No Content):
Entity updated successfully.

### 3.7 Delete Entity
**Endpoint:** `DELETE /api/entities/{entityType}/{id}`  
**Authorization:** Bearer Token Required

**Example:** `DELETE /api/entities/test-agent/507f1f77bcf86cd799439011`

#### Response (204 No Content):
Entity deleted successfully.

### 3.8 Reset Consumed Status
**Endpoint:** `POST /api/entities/{entityType}/{id}/reset`  
**Authorization:** Bearer Token Required  
**Description:** Reset entity's consumed status (marks it as available again)

**Example:** `POST /api/entities/test-agent/507f1f77bcf86cd799439011/reset`

#### Response (200 OK):
```json
{
  "message": "Entity reset successfully"
}
```

### 3.9 Reset All Consumed Entities
**Endpoint:** `POST /api/entities/{entityType}/reset-all?environment={env}`  
**Authorization:** Bearer Token Required  
**Description:** Reset all consumed entities of a type (optionally filtered by environment)

**Example:** `POST /api/entities/test-agent/reset-all?environment=qa`

#### Response (200 OK):
```json
{
  "message": "Reset 25 entities"
}
```

---

## 4. Environments

Environments allow you to segment your test data (dev, staging, qa, production, etc.).

### 4.1 Get All Environments
**Endpoint:** `GET /api/environments?includeInactive={bool}&includeStatistics={bool}`  
**Authorization:** Bearer Token Required

#### Query Parameters:
- `includeInactive` (boolean, default: false): Include inactive environments
- `includeStatistics` (boolean, default: false): Include entity counts per environment

#### Response (200 OK):
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "name": "dev",
    "displayName": "Development",
    "description": "Development environment",
    "url": "https://dev.example.com",
    "color": "#00ff00",
    "order": 1,
    "isActive": true,
    "configuration": {
      "apiTimeout": "30",
      "maxRetries": "3"
    },
    "tags": ["development", "local"],
    "createdAt": "2025-12-16T10:00:00Z",
    "updatedAt": "2025-12-16T10:00:00Z",
    "entityCounts": {
      "test-agent": 45,
      "test-user": 120
    }
  }
]
```

### 4.2 Get Environment by ID
**Endpoint:** `GET /api/environments/{id}?includeStatistics={bool}`  
**Authorization:** Bearer Token Required

### 4.3 Get Environment by Name
**Endpoint:** `GET /api/environments/name/{name}?includeStatistics={bool}`  
**Authorization:** Bearer Token Required

**Example:** `GET /api/environments/name/qa`

### 4.4 Get Environment Statistics
**Endpoint:** `GET /api/environments/{name}/statistics`  
**Authorization:** Bearer Token Required

**Example:** `GET /api/environments/qa/statistics`

#### Response (200 OK):
```json
{
  "environmentName": "qa",
  "totalEntities": 165,
  "entityCounts": {
    "test-agent": 45,
    "test-user": 120
  },
  "consumedCounts": {
    "test-agent": 12,
    "test-user": 8
  }
}
```

### 4.5 Create Environment
**Endpoint:** `POST /api/environments`  
**Authorization:** Bearer Token Required (Admin Role)

#### Request Body:
```json
{
  "name": "qa",
  "displayName": "QA Environment",
  "description": "Quality Assurance testing environment",
  "url": "https://qa.example.com",
  "color": "#0000ff",
  "order": 4,
  "configuration": {
    "apiKey": "qa-api-key",
    "timeout": "30"
  },
  "tags": ["qa", "testing"]
}
```

#### Response (201 Created):
Returns created environment with ID and timestamps.

### 4.6 Update Environment
**Endpoint:** `PUT /api/environments/{id}`  
**Authorization:** Bearer Token Required (Admin Role)

### 4.7 Delete Environment
**Endpoint:** `DELETE /api/environments/{id}`  
**Authorization:** Bearer Token Required (Admin Role)

---

## 5. Settings & API Keys

### 5.1 Get Application Settings
**Endpoint:** `GET /api/settings`  
**Authorization:** Bearer Token Required (Admin Role)

#### Response (200 OK):
```json
{
  "id": "507f1f77bcf86cd799439011",
  "dataRetentionDays": 30,
  "enableNotifications": true,
  "maxEntitiesPerType": 10000,
  "allowParallelExecution": true,
  "updatedAt": "2025-12-16T10:00:00Z",
  "updatedBy": "admin"
}
```

### 5.2 Update Application Settings
**Endpoint:** `PUT /api/settings`  
**Authorization:** Bearer Token Required (Admin Role)

#### Request Body:
Same structure as GET response.

### 5.3 Get API Keys
**Endpoint:** `GET /api/settings/api-keys`  
**Authorization:** Bearer Token Required (Admin Role)

#### Response (200 OK):
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "name": "Integration Key 1",
    "key": "sk_test_abc123...",
    "expiresAt": "2026-12-16T10:00:00Z",
    "createdAt": "2025-12-16T10:00:00Z",
    "createdBy": "admin",
    "lastUsed": "2025-12-16T15:30:00Z",
    "isActive": true
  }
]
```

### 5.4 Create API Key
**Endpoint:** `POST /api/settings/api-keys`  
**Authorization:** Bearer Token Required (Admin Role)

#### Request Body:
```json
{
  "name": "Integration Key 1",
  "expiresInDays": 365
}
```

#### Response (201 Created):
Returns created API key with generated key value.

### 5.5 Delete API Key
**Endpoint:** `DELETE /api/settings/api-keys/{id}`  
**Authorization:** Bearer Token Required (Admin Role)

---

## 6. Users

### 6.1 Get All Users
**Endpoint:** `GET /api/users`  
**Authorization:** Bearer Token Required (Admin Role)

#### Response (200 OK):
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "admin",
    "email": "admin@example.com",
    "role": "Admin",
    "isActive": true,
    "createdAt": "2025-12-16T10:00:00Z",
    "lastLogin": "2025-12-16T15:30:00Z"
  }
]
```

### 6.2 Get User by ID
**Endpoint:** `GET /api/users/{id}`  
**Authorization:** Bearer Token Required (Admin Role)

### 6.3 Get User by Username
**Endpoint:** `GET /api/users/username/{username}`  
**Authorization:** Bearer Token Required (Admin Role)

### 6.4 Create User
**Endpoint:** `POST /api/users`  
**Authorization:** Bearer Token Required (Admin Role)

#### Request Body:
```json
{
  "username": "testuser",
  "email": "test@example.com",
  "password": "SecurePass@123",
  "role": "User"
}
```

#### Roles:
- `Admin` - Full access
- `User` - Read/Write access to entities
- `ReadOnly` - Read-only access

### 6.5 Update User
**Endpoint:** `PUT /api/users/{id}`  
**Authorization:** Bearer Token Required (Admin Role)

### 6.6 Delete User
**Endpoint:** `DELETE /api/users/{id}`  
**Authorization:** Bearer Token Required (Admin Role)

### 6.7 Change Password
**Endpoint:** `POST /api/users/{id}/change-password`  
**Authorization:** Bearer Token Required

#### Request Body:
```json
{
  "currentPassword": "OldPass@123",
  "newPassword": "NewPass@123"
}
```

---

## 7. Data Models

### 7.1 DynamicEntity
```typescript
{
  id: string;                          // MongoDB ObjectId
  entityType: string;                  // Schema name (e.g., "test-agent")
  environment: string | null;          // Environment name or null
  fields: Record<string, any>;         // Dynamic fields based on schema
  isConsumed: boolean;                 // Consumed status
  createdAt: string;                   // ISO 8601 datetime
  updatedAt: string;                   // ISO 8601 datetime
}
```

### 7.2 EntitySchema
```typescript
{
  id: string;
  entityName: string;                  // Unique entity type name
  fields: FieldDefinition[];           // Field definitions
  filterableFields: string[];          // Fields available for filtering
  uniqueFields: string[];              // Compound unique constraint fields
  useCompoundUnique: boolean;          // Treat uniqueFields as compound key
  excludeOnFetch: boolean;             // Auto-consume on fetch
  createdAt: string;
  updatedAt: string;
}
```

### 7.3 FieldDefinition
```typescript
{
  name: string;                        // Field name
  type: "string" | "number" | "boolean" | "date" | "array" | "object";
  required: boolean;                   // Field is required
  isUnique: boolean;                   // Field must be unique
  description: string | null;          // Optional description
}
```

### 7.4 Environment
```typescript
{
  id: string;
  name: string;                        // Unique name (lowercase, alphanumeric)
  displayName: string;                 // Display name
  description: string;                 // Description
  url: string | null;                  // Environment URL
  color: string;                       // Hex color code
  order: number;                       // Display order
  isActive: boolean;                   // Active status
  configuration: Record<string, string>; // Key-value config
  tags: string[];                      // Tags
  createdAt: string;
  updatedAt: string;
  entityCounts?: Record<string, number>; // Optional: entity counts per type
}
```

### 7.5 LoginRequest
```typescript
{
  username: string;
  password: string;
}
```

### 7.6 LoginResponse
```typescript
{
  token: string;                       // JWT token
  user: {
    id: string;
    username: string;
    email: string;
    role: string;
    createdAt: string;
  };
}
```

---

## 8. Error Handling

### HTTP Status Codes:
- `200 OK` - Request succeeded
- `201 Created` - Resource created successfully
- `204 No Content` - Request succeeded (no response body)
- `400 Bad Request` - Invalid request (validation error)
- `401 Unauthorized` - Missing or invalid authentication
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource already exists or conflict
- `500 Internal Server Error` - Server error

### Error Response Format:
```json
{
  "error": "Error message describing what went wrong"
}
```

### Common Errors:

#### Validation Errors (400):
```json
{
  "error": "Entity does not match schema for type: test-agent"
}
```

#### Unique Constraint Violation (400):
```json
{
  "error": "Entity does not match schema for type: test-agent"
}
```
*Check logs for specific field that violated uniqueness*

#### Authentication Error (401):
```json
{
  "error": "Invalid username or password"
}
```

#### Not Found (404):
```json
{
  "error": "Entity type 'unknown-type' not found. Create a schema first."
}
```

---

## 9. Integration Examples

### 9.1 C# Client Example

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class TestServiceClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public TestServiceClient(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    // Login and get token
    public async Task<bool> LoginAsync(string username, string password)
    {
        var request = new { username, password };
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
        
        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        _token = result?.Token;
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _token);
        
        return true;
    }

    // Create schema
    public async Task<EntitySchema?> CreateSchemaAsync(EntitySchema schema)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/schemas", schema);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EntitySchema>();
    }

    // Create entity
    public async Task<DynamicEntity?> CreateEntityAsync(
        string entityType, 
        Dictionary<string, object> fields, 
        string? environment = null)
    {
        var entity = new 
        { 
            fields, 
            environment 
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/entities/{entityType}", 
            entity
        );
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DynamicEntity>();
    }

    // Get next available entity (for parallel tests)
    public async Task<DynamicEntity?> GetNextEntityAsync(
        string entityType, 
        string? environment = null)
    {
        var url = $"/api/entities/{entityType}/next";
        if (environment != null)
            url += $"?environment={environment}";
        
        var response = await _httpClient.GetAsync(url);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DynamicEntity>();
    }

    // Get all entities
    public async Task<List<DynamicEntity>> GetEntitiesAsync(
        string entityType, 
        string? environment = null)
    {
        var url = $"/api/entities/{entityType}";
        if (environment != null)
            url += $"?environment={environment}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<List<DynamicEntity>>() 
            ?? new List<DynamicEntity>();
    }

    // Delete entity
    public async Task<bool> DeleteEntityAsync(string entityType, string id)
    {
        var response = await _httpClient.DeleteAsync(
            $"/api/entities/{entityType}/{id}"
        );
        return response.IsSuccessStatusCode;
    }

    // Reset all consumed entities
    public async Task<int> ResetAllEntitiesAsync(
        string entityType, 
        string? environment = null)
    {
        var url = $"/api/entities/{entityType}/reset-all";
        if (environment != null)
            url += $"?environment={environment}";
        
        var response = await _httpClient.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<ResetResponse>();
        return result?.ResetCount ?? 0;
    }
}

// Models
public class LoginResponse
{
    public string Token { get; set; } = "";
    public UserInfo User { get; set; } = new();
}

public class UserInfo
{
    public string Id { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class EntitySchema
{
    public string? Id { get; set; }
    public string EntityName { get; set; } = "";
    public List<FieldDefinition> Fields { get; set; } = new();
    public List<string> FilterableFields { get; set; } = new();
    public bool ExcludeOnFetch { get; set; }
}

public class FieldDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public bool IsUnique { get; set; }
}

public class DynamicEntity
{
    public string? Id { get; set; }
    public string EntityType { get; set; } = "";
    public string? Environment { get; set; }
    public Dictionary<string, object> Fields { get; set; } = new();
    public bool IsConsumed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ResetResponse
{
    public string Message { get; set; } = "";
    public int ResetCount { get; set; }
}
```

### 9.2 Usage Example

```csharp
// Initialize client
var client = new TestServiceClient("http://localhost:5000");

// Login
await client.LoginAsync("admin", "Admin@123");

// Create schema
var schema = new EntitySchema
{
    EntityName = "test-user",
    Fields = new List<FieldDefinition>
    {
        new() { Name = "username", Type = "string", Required = true, IsUnique = true },
        new() { Name = "email", Type = "string", Required = true, IsUnique = true },
        new() { Name = "age", Type = "number", Required = false }
    },
    FilterableFields = new List<string> { "username", "email" },
    ExcludeOnFetch = true // Enable for parallel test execution
};

await client.CreateSchemaAsync(schema);

// Create entities
var entity1 = await client.CreateEntityAsync(
    "test-user",
    new Dictionary<string, object>
    {
        ["username"] = "user1",
        ["email"] = "user1@example.com",
        ["age"] = 25
    },
    environment: "qa"
);

// Get next available entity (parallel-safe)
var testUser = await client.GetNextEntityAsync("test-user", "qa");
if (testUser != null)
{
    // Use the entity in your test
    Console.WriteLine($"Using entity: {testUser.Fields["username"]}");
    // Entity is automatically marked as consumed
}

// Get all entities
var allUsers = await client.GetEntitiesAsync("test-user", "qa");

// Reset consumed entities after test run
var resetCount = await client.ResetAllEntitiesAsync("test-user", "qa");
Console.WriteLine($"Reset {resetCount} entities");
```

### 9.3 Parallel Test Execution Pattern

```csharp
[Test]
[Parallelizable(ParallelScope.All)]
public async Task Test_UserLogin_WithUniqueAccount()
{
    var client = new TestServiceClient("http://localhost:5000");
    await client.LoginAsync("admin", "Admin@123");
    
    // Get unique test user (thread-safe)
    var testUser = await client.GetNextEntityAsync("test-user", "qa");
    
    if (testUser == null)
    {
        Assert.Fail("No available test users");
        return;
    }
    
    // Use the test user
    var username = testUser.Fields["username"].ToString();
    var password = testUser.Fields["password"].ToString();
    
    // Run your test with unique credentials
    var loginResult = await YourApp.LoginAsync(username, password);
    Assert.That(loginResult.IsSuccess, Is.True);
    
    // Entity remains marked as consumed - won't be used by parallel tests
}

[OneTimeTearDown]
public async Task ResetTestData()
{
    var client = new TestServiceClient("http://localhost:5000");
    await client.LoginAsync("admin", "Admin@123");
    
    // Reset all consumed entities for next test run
    await client.ResetAllEntitiesAsync("test-user", "qa");
}
```

---

## 10. Best Practices

### 10.1 Schema Design
- ✅ Use descriptive entity names (e.g., "test-agent", "user-account")
- ✅ Mark fields as `required` if they must always be provided
- ✅ Mark fields as `isUnique` for fields like username, email, userId
- ✅ Add fields to `filterableFields` if you'll search by them
- ✅ Set `excludeOnFetch: true` for parallel test execution scenarios

### 10.2 Uniqueness
- Individual fields with `isUnique: true` are validated independently
- Each unique field must have a distinct value across all entities
- Uniqueness is scoped to the environment
- Use compound unique keys via `uniqueFields` array when needed

### 10.3 Parallel Testing
- Use schemas with `excludeOnFetch: true`
- Use `GET /api/entities/{type}/next` to atomically fetch entities
- Reset consumed entities with `/reset-all` after test runs
- Each test gets a unique entity without conflicts

### 10.4 Environment Strategy
- Use separate environments for different test stages
- `dev` - Development/local testing
- `qa` - QA team testing
- `staging` - Pre-production testing
- `prod` - Production (if applicable)

### 10.5 Security
- Always use HTTPS in production
- Store JWT tokens securely
- Rotate API keys regularly
- Use role-based access control

---

## 11. Troubleshooting

### Issue: "Entity does not match schema"
**Cause:** Validation failure  
**Solution:** 
- Check all required fields are provided
- Verify unique fields don't have duplicates
- Ensure field types match schema definition

### Issue: "No available entities found"
**Cause:** All entities are consumed  
**Solution:**
- Call `/reset-all` endpoint to reset consumed status
- Create more entities
- Check environment filter

### Issue: 401 Unauthorized
**Cause:** Missing or invalid token  
**Solution:**
- Login again to get a fresh token
- Ensure token is included in Authorization header
- Check token hasn't expired

### Issue: 404 Entity type not found
**Cause:** Schema doesn't exist  
**Solution:**
- Create the schema first using POST /api/schemas
- Verify schema name spelling

---

## 12. Support & Contact

**Documentation Version:** 1.0.0  
**API Version:** 1.0.0  
**Last Updated:** December 16, 2025

For issues or questions:
- Check logs: `docker compose logs api`
- Review this documentation
- Verify authentication and permissions

---

## Appendix A: Complete cURL Examples

### Login
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}'
```

### Create Schema
```bash
curl -X POST http://localhost:5000/api/schemas \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "entityName": "test-agent",
    "fields": [
      {"name": "username", "type": "string", "required": true, "isUnique": true},
      {"name": "userId", "type": "number", "required": true, "isUnique": true}
    ],
    "excludeOnFetch": true
  }'
```

### Create Entity
```bash
curl -X POST http://localhost:5000/api/entities/test-agent \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "fields": {
      "username": "user123",
      "userId": 12345
    },
    "environment": "qa"
  }'
```

### Get Next Available
```bash
curl -X GET "http://localhost:5000/api/entities/test-agent/next?environment=qa" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Reset All
```bash
curl -X POST "http://localhost:5000/api/entities/test-agent/reset-all?environment=qa" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

**End of Documentation**
