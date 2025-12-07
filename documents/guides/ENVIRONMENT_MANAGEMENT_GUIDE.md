# Environment Management Guide

## Overview

The Environment Management feature allows you to organize test data across multiple environments (e.g., dev, staging, production), providing complete isolation and control over test data for different testing scenarios.

---

## Features

? **Multi-Environment Support** - Separate test data for different environments  
? **Environment CRUD** - Create, read, update, and delete environments  
? **Environment Statistics** - Track entity counts and usage per environment  
? **Environment Filtering** - Filter entities by environment  
? **Default Environments** - Auto-created dev, staging, production environments  
? **Configuration Storage** - Store environment-specific configuration  
? **Visual Organization** - Assign colors and ordering to environments  

---

## Concepts

### Environment
An environment is a logical container for organizing test data. Each environment can have:
- **Name**: Unique identifier (lowercase, alphanumeric, hyphens)
- **Display Name**: Human-readable name
- **Description**: Purpose and usage notes
- **URL**: Associated endpoint or URL
- **Color**: Visual identification (#hex color)
- **Order**: Display ordering
- **Configuration**: Key-value pairs for environment-specific settings
- **Tags**: Categorization and filtering
- **Active Status**: Enable/disable environments

### Environment Association
Entities can be associated with an environment by setting the `environment` field when creating or updating them.

---

## Default Environments

Automatically created on first application start:

| Name | Display Name | Color | Description |
|------|--------------|-------|-------------|
| dev | Development | #00ff00 (Green) | Development environment for testing new features |
| staging | Staging | #ffa500 (Orange) | Pre-production testing environment |
| production | Production | #ff0000 (Red) | Production environment |

---

## API Endpoints

### Get All Environments

```bash
GET /api/environments
Authorization: Bearer {token}

# Query parameters:
# - includeInactive: Include inactive environments (default: false)
# - includeStatistics: Include entity statistics (default: false)

curl -X GET "https://localhost:5001/api/environments?includeStatistics=true" \
  -H "Authorization: Bearer {token}" \
  -k
```

**Response:**
```json
[
  {
    "id": "67788a1b2c3d4e5f6g7h8i9j",
    "name": "dev",
    "displayName": "Development",
    "description": "Development environment for testing new features",
    "url": null,
    "color": "#00ff00",
    "isActive": true,
    "order": 1,
    "configuration": {},
    "tags": ["development", "testing"],
    "createdAt": "2025-01-06T10:00:00Z",
    "updatedAt": "2025-01-06T10:00:00Z",
    "createdBy": "system",
    "statistics": {
      "totalEntities": 150,
      "availableEntities": 120,
      "consumedEntities": 30,
      "entitiesByType": {
        "Agent": 100,
        "Customer": 50
      },
      "lastActivity": "2025-01-06T12:30:00Z"
    }
  }
]
```

### Get Environment by ID

```bash
GET /api/environments/{id}
Authorization: Bearer {token}

curl -X GET "https://localhost:5001/api/environments/67788a1b2c3d4e5f6g7h8i9j" \
  -H "Authorization: Bearer {token}" \
  -k
```

### Get Environment by Name

```bash
GET /api/environments/name/{name}
Authorization: Bearer {token}

curl -X GET "https://localhost:5001/api/environments/name/dev" \
  -H "Authorization: Bearer {token}" \
  -k
```

### Get Environment Statistics

```bash
GET /api/environments/{name}/statistics
Authorization: Bearer {token}

curl -X GET "https://localhost:5001/api/environments/dev/statistics" \
  -H "Authorization: Bearer {token}" \
  -k
```

**Response:**
```json
{
  "totalEntities": 150,
  "availableEntities": 120,
  "consumedEntities": 30,
  "entitiesByType": {
    "Agent": 100,
    "Customer": 50
  },
  "lastActivity": "2025-01-06T12:30:00Z"
}
```

### Create Environment (Admin Only)

```bash
POST /api/environments
Authorization: Bearer {admin-token}
Content-Type: application/json

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

**Validation Rules:**
- Name must be lowercase alphanumeric with hyphens only
- Name must be unique
- Name cannot be changed after creation

### Update Environment (Admin Only)

```bash
PUT /api/environments/{id}
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "displayName": "QA Updated",
  "description": "Updated description",
  "url": "https://qa-new.example.com",
  "isActive": true,
  "order": 5,
  "tags": ["qa", "testing", "updated"]
}
```

**Note:** All fields are optional. Only provided fields will be updated.

### Delete Environment (Admin Only)

```bash
DELETE /api/environments/{id}
Authorization: Bearer {admin-token}

curl -X DELETE "https://localhost:5001/api/environments/67788a1b2c3d4e5f6g7h8i9j" \
  -H "Authorization: Bearer {admin-token}" \
  -k
```

**Important:** Cannot delete an environment that contains entities. Delete all entities first.

### Activate/Deactivate Environment (Admin Only)

```bash
# Activate
POST /api/environments/{id}/activate
Authorization: Bearer {admin-token}

# Deactivate
POST /api/environments/{id}/deactivate
Authorization: Bearer {admin-token}
```

---

## Working with Entities in Environments

### Create Entity with Environment

```bash
POST /api/entities/Agent
Authorization: Bearer {token}
Content-Type: application/json

{
  "environment": "dev",
  "fields": {
    "username": "john.doe",
    "password": "SecurePass@123",
    "brandId": "brand123"
  }
}
```

### Get Entities by Environment

```bash
# Get all entities in 'dev' environment
GET /api/entities/Agent?environment=dev
Authorization: Bearer {token}

curl -X GET "https://localhost:5001/api/entities/Agent?environment=dev" \
  -H "Authorization: Bearer {token}" \
  -k
```

### Get Next Available Entity in Environment

```bash
# For parallel test execution with environment isolation
GET /api/entities/Agent/next?environment=dev
Authorization: Bearer {token}

curl -X GET "https://localhost:5001/api/entities/Agent/next?environment=dev" \
  -H "Authorization: Bearer {token}" \
  -k
```

### Filter by Field and Environment

```bash
# Get agents by brandId in 'staging' environment
GET /api/entities/Agent/filter/brandId/brand123?environment=staging
Authorization: Bearer {token}
```

### Reset Consumed Entities in Environment

```bash
POST /api/entities/Agent/reset-all?environment=dev
Authorization: Bearer {token}

curl -X POST "https://localhost:5001/api/entities/Agent/reset-all?environment=dev" \
  -H "Authorization: Bearer {token}" \
  -k
```

---

## PowerShell Examples

### Setup

```powershell
# Login and get token
$loginBody = @{
    username = "admin"
    password = "Admin@123"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/login" `
  -Method Post -Body $loginBody -ContentType "application/json" `
  -SkipCertificateCheck

$token = $response.token
$headers = @{ Authorization = "Bearer $token" }
```

### List All Environments

```powershell
$environments = Invoke-RestMethod `
  -Uri "https://localhost:5001/api/environments?includeStatistics=true" `
  -Headers $headers -SkipCertificateCheck

$environments | Format-Table Name, DisplayName, IsActive, TotalEntities
```

### Create QA Environment

```powershell
$newEnv = @{
    name = "qa"
    displayName = "QA Environment"
    description = "Quality Assurance testing"
    url = "https://qa.example.com"
    color = "#0000ff"
    order = 4
    configuration = @{
        apiKey = "qa-key-123"
        timeout = "30"
    }
    tags = @("qa", "testing")
} | ConvertTo-Json

$qaEnv = Invoke-RestMethod -Uri "https://localhost:5001/api/environments" `
  -Method Post -Headers $headers -Body $newEnv `
  -ContentType "application/json" -SkipCertificateCheck

Write-Host "Created environment: $($qaEnv.name)"
```

### Get Environment Statistics

```powershell
$stats = Invoke-RestMethod `
  -Uri "https://localhost:5001/api/environments/dev/statistics" `
  -Headers $headers -SkipCertificateCheck

Write-Host "Dev Environment Statistics:"
Write-Host "  Total Entities: $($stats.totalEntities)"
Write-Host "  Available: $($stats.availableEntities)"
Write-Host "  Consumed: $($stats.consumedEntities)"
Write-Host "  Last Activity: $($stats.lastActivity)"
```

### Create Entities in Different Environments

```powershell
# Create 10 agents in 'dev'
1..10 | ForEach-Object {
    $agent = @{
        environment = "dev"
        fields = @{
            username = "dev_user_$_"
            password = "DevPass@123"
            userId = "dev_$_"
            brandId = "brand_dev"
        }
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "https://localhost:5001/api/entities/Agent" `
      -Method Post -Headers $headers -Body $agent `
      -ContentType "application/json" -SkipCertificateCheck
}

# Create 10 agents in 'staging'
1..10 | ForEach-Object {
    $agent = @{
        environment = "staging"
        fields = @{
            username = "staging_user_$_"
            password = "StagingPass@123"
            userId = "staging_$_"
            brandId = "brand_staging"
        }
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "https://localhost:5001/api/entities/Agent" `
      -Method Post -Headers $headers -Body $agent `
      -ContentType "application/json" -SkipCertificateCheck
}

Write-Host "Created 10 agents in 'dev' and 10 in 'staging'"
```

### Query Entities by Environment

```powershell
# Get all dev agents
$devAgents = Invoke-RestMethod `
  -Uri "https://localhost:5001/api/entities/Agent?environment=dev" `
  -Headers $headers -SkipCertificateCheck

Write-Host "Dev agents: $($devAgents.Count)"

# Get all staging agents
$stagingAgents = Invoke-RestMethod `
  -Uri "https://localhost:5001/api/entities/Agent?environment=staging" `
  -Headers $headers -SkipCertificateCheck

Write-Host "Staging agents: $($stagingAgents.Count)"
```

---

## Use Cases

### 1. Parallel Test Execution Across Environments

```bash
# Test Suite 1: Uses 'dev' environment
GET /api/entities/Agent/next?environment=dev

# Test Suite 2: Uses 'staging' environment
GET /api/entities/Agent/next?environment=staging

# Test Suite 3: Uses 'production' environment  
GET /api/entities/Agent/next?environment=production
```

**Benefit:** Complete isolation between test suites running in parallel.

### 2. Environment-Specific Test Data

```bash
# Create dev-specific test data
POST /api/entities/Agent
{
  "environment": "dev",
  "fields": {
    "username": "dev_tester",
    "brandId": "dev_brand"
  }
}

# Create production-like test data
POST /api/entities/Agent
{
  "environment": "production",
  "fields": {
    "username": "prod_tester",
    "brandId": "prod_brand"
  }
}
```

### 3. Progressive Testing

```bash
# 1. Test in dev
GET /api/entities/Agent/next?environment=dev

# 2. If successful, promote to staging
GET /api/entities/Agent/next?environment=staging

# 3. Final validation in production
GET /api/entities/Agent/next?environment=production
```

### 4. Environment Cleanup

```bash
# Reset only dev environment after testing
POST /api/entities/Agent/reset-all?environment=dev

# Other environments remain untouched
```

---

## Best Practices

### DO ?

1. **Use meaningful environment names** (dev, staging, prod, qa, uat)
2. **Assign colors for visual identification**
3. **Set appropriate ordering** for display
4. **Include environment in entity creation**
5. **Filter by environment** in queries
6. **Use environment-specific configuration**
7. **Track statistics** for monitoring
8. **Document environment purpose** in description
9. **Tag environments** for categorization
10. **Reset consumed entities per environment**

### DON'T ?

1. **Don't use special characters** in environment names
2. **Don't create too many environments** (keep it manageable)
3. **Don't mix test data** across environments
4. **Don't forget to specify environment** when creating entities
5. **Don't delete environments** with existing entities
6. **Don't use production environment** for destructive tests
7. **Don't hardcode environment names** in tests
8. **Don't skip environment statistics** monitoring
9. **Don't ignore inactive environments** (clean up)
10. **Don't use uppercase** in environment names

---

## Environment Configuration

Store environment-specific settings in the `configuration` field:

```json
{
  "name": "qa",
  "configuration": {
    "apiUrl": "https://qa-api.example.com",
    "apiKey": "qa-secret-key",
    "timeout": "30",
    "retryAttempts": "3",
    "database": "qa_database",
    "logLevel": "debug"
  }
}
```

Access in tests:
```bash
GET /api/environments/name/qa
# Returns configuration for use in tests
```

---

## Monitoring & Statistics

### Track Environment Usage

```bash
# Get detailed statistics
GET /api/environments?includeStatistics=true

# Monitor specific environment
GET /api/environments/dev/statistics
```

### Key Metrics

- **Total Entities**: All entities in environment
- **Available Entities**: Ready for consumption
- **Consumed Entities**: Already used
- **Entities by Type**: Breakdown by entity type
- **Last Activity**: Most recent entity update

---

## Migration Guide

### For Existing Entities

Entities without an environment field are considered "global" and can be accessed without environment filtering.

To migrate existing entities to environments:

```powershell
# Get all entities without environment
$entities = Invoke-RestMethod `
  -Uri "https://localhost:5001/api/entities/Agent" `
  -Headers $headers -SkipCertificateCheck

# Update each entity with environment
foreach ($entity in $entities) {
    if (-not $entity.environment) {
        $entity.environment = "dev"
        
        Invoke-RestMethod `
          -Uri "https://localhost:5001/api/entities/Agent/$($entity.id)" `
          -Method Put -Headers $headers -Body ($entity | ConvertTo-Json) `
          -ContentType "application/json" -SkipCertificateCheck
    }
}
```

---

## Troubleshooting

### Problem: Environment statistics show 0 entities but entities exist

**Solution:** Entities may not have environment field set. Update entities or query without environment filter.

### Problem: Cannot delete environment

**Solution:** Environment contains entities. Delete all entities first or move them to another environment.

### Problem: Entities not showing in environment query

**Solution:** Verify entity has correct environment field. Check environment name spelling.

### Problem: "Environment name must be lowercase"

**Solution:** Use lowercase only: `dev`, not `Dev` or `DEV`.

---

## Schema & Models

### Environment Model

```csharp
{
  "id": "string",
  "name": "string",              // Required, unique, lowercase
  "displayName": "string",        // Required
  "description": "string",        // Optional
  "url": "string",               // Optional
  "color": "string",             // Optional, hex color
  "isActive": "boolean",         // Default: true
  "order": "number",             // Display order
  "configuration": "object",      // Key-value pairs
  "tags": "array",               // String array
  "createdAt": "datetime",
  "updatedAt": "datetime",
  "createdBy": "string"
}
```

### Environment Statistics Model

```csharp
{
  "totalEntities": "number",
  "availableEntities": "number",
  "consumedEntities": "number",
  "entitiesByType": "object",    // { "Agent": 100, "Customer": 50 }
  "lastActivity": "datetime"
}
```

---

## Integration with CI/CD

### Example: GitHub Actions

```yaml
- name: Create test environment
  run: |
    curl -X POST https://api.example.com/api/environments \
      -H "Authorization: Bearer ${{ secrets.API_TOKEN }}" \
      -H "Content-Type: application/json" \
      -d '{"name":"ci-${{ github.run_id }}","displayName":"CI Run ${{ github.run_id }}"}'

- name: Run tests with environment
  run: |
    pytest --environment=ci-${{ github.run_id }}

- name: Cleanup environment
  run: |
    curl -X DELETE https://api.example.com/api/environments/name/ci-${{ github.run_id }} \
      -H "Authorization: Bearer ${{ secrets.API_TOKEN }}"
```

---

**Environment organization keeps your test data clean, isolated, and manageable!** ???

**See Also:**
- [User Management Guide](USER_MANAGEMENT_GUIDE.md)
- [Dynamic System Guide](DYNAMIC_SYSTEM_GUIDE.md)
- [Parallel Test Execution Guide](PARALLEL_TEST_EXECUTION_GUIDE.md)
