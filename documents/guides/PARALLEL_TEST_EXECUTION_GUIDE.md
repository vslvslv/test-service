# Parallel Test Execution Feature - ExcludeOnFetch

## Overview

The **ExcludeOnFetch** feature allows test objects to be automatically marked as "consumed" when fetched, preventing them from being returned in subsequent queries. This is essential for parallel test execution to avoid test conflicts when multiple tests try to use the same test data.

---

## How It Works

### 1. Enable ExcludeOnFetch in Schema

When creating a schema, set `excludeOnFetch: true`:

```json
POST /api/schemas
{
  "entityName": "Agent",
  "fields": [
    { "name": "username", "type": "string", "required": true },
    { "name": "brandId", "type": "string", "required": false }
  ],
  "filterableFields": ["username", "brandId"],
  "excludeOnFetch": true
}
```

### 2. Automatic Behavior

When `excludeOnFetch` is enabled:

- **GET /api/entities/Agent** - Returns only non-consumed entities
- **GET /api/entities/Agent/{id}** - Returns the entity AND marks it as consumed
- **GET /api/entities/Agent/filter/brandId/brand123** - Returns only non-consumed entities matching the filter
- **GET /api/entities/Agent/next** - Atomically gets next available and marks as consumed

### 3. Test Data Lifecycle

```
[Create] ? [Available] ? [Fetch] ? [Consumed] ? [Reset] ? [Available]
```

---

## API Endpoints

### Get Next Available Entity (Recommended for Parallel Tests)

**GET** `/api/entities/{entityType}/next`

This is the **recommended** endpoint for parallel test execution. It atomically finds and marks an entity as consumed in a single database operation, preventing race conditions.

**Example:**
```bash
curl -X GET https://localhost:5001/api/entities/Agent/next -k
```

**Response:**
```json
{
  "id": "507f1f77bcf86cd799439011",
  "entityType": "Agent",
  "fields": {
    "username": "john.doe",
    "brandId": "brand123"
  },
  "isConsumed": true,
  "createdAt": "2025-01-06T10:30:00Z",
  "updatedAt": "2025-01-06T10:35:00Z"
}
```

**Benefits:**
- ? Thread-safe (atomic operation)
- ? No race conditions
- ? Guaranteed unique entity per test
- ? Works in high-concurrency scenarios

---

### Reset Consumed Entity

**POST** `/api/entities/{entityType}/{id}/reset`

Resets the consumed flag for a specific entity, making it available again.

**Example:**
```bash
curl -X POST https://localhost:5001/api/entities/Agent/507f1f77bcf86cd799439011/reset -k
```

**Use Case:**
- Reset a specific entity after test completion
- Reuse entities in sequential test runs

---

### Reset All Consumed Entities

**POST** `/api/entities/{entityType}/reset-all`

Resets all consumed entities of a type, making them all available again.

**Example:**
```bash
curl -X POST https://localhost:5001/api/entities/Agent/reset-all -k
```

**Response:**
```json
{
  "resetCount": 15,
  "message": "Reset 15 consumed entities"
}
```

**Use Cases:**
- Clean up after test suite completion
- Prepare test data for next test run
- Reset all entities between test cycles

---

## Usage Patterns

### Pattern 1: Parallel Test Execution (Best Practice)

```csharp
[Test]
[Parallelizable(ParallelScope.All)]
public async Task MyTest()
{
    // Get next available agent - guaranteed unique across parallel tests
    var response = await _client.GetAsync("/api/entities/Agent/next");
    var agent = await response.Content.ReadFromJsonAsync<DynamicEntity>();
    
    // Use agent in your test
    Assert.That(agent.Fields["username"], Is.Not.Null);
    
    // No cleanup needed - entity is already marked as consumed
}
```

### Pattern 2: Test Suite Setup/Teardown

```csharp
[SetUp]
public void Setup()
{
    // Reset all entities before test run
    _client.PostAsync("/api/entities/Agent/reset-all", null).Wait();
}

[TearDown]
public void TearDown()
{
    // Optional: Reset again after tests
    _client.PostAsync("/api/entities/Agent/reset-all", null).Wait();
}

[Test]
public async Task MyTest()
{
    var agent = await GetNextAgent();
    // Use agent...
}
```

### Pattern 3: Create Test Data On-Demand

```csharp
[Test]
public async Task MyTest()
{
    // Create test agent
    var agent = new DynamicEntity
    {
        Fields = new Dictionary<string, object?>
        {
            { "username", $"testuser_{Guid.NewGuid()}" },
            { "brandId", "brand123" }
        }
    };
    
    await _client.PostAsJsonAsync("/api/entities/Agent", agent);
    
    // Immediately get it (will be marked as consumed)
    var response = await _client.GetAsync("/api/entities/Agent/next");
    var fetchedAgent = await response.Content.ReadFromJsonAsync<DynamicEntity>();
    
    // Use in test...
}
```

---

## PowerShell Examples

### Create Schema with ExcludeOnFetch

```powershell
$schema = @{
    entityName = "Agent"
    fields = @(
        @{ name = "username"; type = "string"; required = $true }
        @{ name = "brandId"; type = "string"; required = $false }
    )
    filterableFields = @("username", "brandId")
    excludeOnFetch = $true
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "https://localhost:5001/api/schemas" `
  -Method Post -Body $schema -ContentType "application/json" `
  -SkipCertificateCheck
```

### Get Next Available

```powershell
$agent = Invoke-RestMethod -Uri "https://localhost:5001/api/entities/Agent/next" `
  -SkipCertificateCheck

Write-Host "Got agent: $($agent.fields.username)"
```

### Reset All After Tests

```powershell
$result = Invoke-RestMethod -Uri "https://localhost:5001/api/entities/Agent/reset-all" `
  -Method Post -SkipCertificateCheck

Write-Host "Reset $($result.resetCount) agents"
```

---

## Behavior Comparison

### Without ExcludeOnFetch (excludeOnFetch: false)

```
Test 1: GET /api/entities/Agent ? Returns Agent A
Test 2: GET /api/entities/Agent ? Returns Agent A (SAME!)
Result: Both tests use same agent ? CONFLICT!
```

### With ExcludeOnFetch (excludeOnFetch: true)

```
Test 1: GET /api/entities/Agent/next ? Returns Agent A (marks as consumed)
Test 2: GET /api/entities/Agent/next ? Returns Agent B (Agent A excluded)
Result: Each test gets unique agent ? NO CONFLICT!
```

---

## Database Implementation

### Entity Storage

Entities include an `isConsumed` flag:

```json
{
  "_id": ObjectId("507f1f77bcf86cd799439011"),
  "entityType": "Agent",
  "username": "john.doe",
  "brandId": "brand123",
  "isConsumed": false,
  "createdAt": ISODate("2025-01-06T10:30:00Z"),
  "updatedAt": ISODate("2025-01-06T10:30:00Z")
}
```

### Atomic Operation

The `/next` endpoint uses MongoDB's `FindOneAndUpdate` for atomic operations:

```javascript
db.Dynamic_Agent.findOneAndUpdate(
  { $or: [{ isConsumed: false }, { isConsumed: { $exists: false } }] },
  { $set: { isConsumed: true, updatedAt: new Date() } },
  { returnDocument: "after" }
)
```

This ensures **no race conditions** even with hundreds of parallel tests.

---

## Message Bus Events

When entities are consumed, a message is published to RabbitMQ:

**Event:** `{entitytype}.consumed`

**Example:**
```json
{
  "EntityType": "Agent",
  "Id": "507f1f77bcf86cd799439011",
  "Action": "Consumed"
}
```

---

## Best Practices

### ? DO

1. **Use `/next` endpoint for parallel tests**
   - Atomic operation prevents race conditions
   - Thread-safe by design

2. **Reset entities between test runs**
   - Use `reset-all` in test suite setup/teardown
   - Keeps test data clean

3. **Create enough test data**
   - Ensure you have more entities than parallel tests
   - Prevents "No available entities" errors

4. **Use excludeOnFetch only for test objects**
   - Production data shouldn't use this feature
   - Only for test/demo scenarios

### ? DON'T

1. **Don't use regular GET endpoints in parallel tests**
   - Use `/next` instead for guaranteed uniqueness

2. **Don't forget to reset after failures**
   - Failed tests leave entities consumed
   - Use try-finally or teardown methods

3. **Don't enable excludeOnFetch for production entities**
   - Only use for test data
   - Not suitable for normal application data

---

## Troubleshooting

### Problem: "No available entities found"

**Cause:** All entities are consumed

**Solutions:**
1. Reset all entities: `POST /api/entities/{type}/reset-all`
2. Create more test entities
3. Check if entities exist: `GET /api/entities/{type}` (includes consumed)

### Problem: Tests still conflicting

**Cause:** Not using `/next` endpoint

**Solution:** Use `/api/entities/{type}/next` instead of regular GET endpoints

### Problem: Entities not being excluded

**Cause:** Schema doesn't have `excludeOnFetch: true`

**Solution:** Update schema or create new schema with flag enabled

---

## Performance Considerations

### Atomic Operations
- `/next` endpoint uses atomic `FindOneAndUpdate`
- No performance penalty for atomic operation
- Scales well with concurrent requests

### Indexing
Recommend creating index on `isConsumed` field for large datasets:

```javascript
db.Dynamic_Agent.createIndex({ isConsumed: 1 })
```

### Cleanup
- Regular resets don't impact performance
- Use `reset-all` during off-peak hours for large datasets

---

## Complete Example

### 1. Create Schema

```bash
curl -X POST https://localhost:5001/api/schemas \
  -H "Content-Type: application/json" \
  -d '{
    "entityName": "Agent",
    "fields": [
      {"name": "username", "type": "string", "required": true},
      {"name": "brandId", "type": "string", "required": false}
    ],
    "filterableFields": ["username", "brandId"],
    "excludeOnFetch": true
  }' -k
```

### 2. Create Test Data

```bash
for i in {1..10}; do
  curl -X POST https://localhost:5001/api/entities/Agent \
    -H "Content-Type: application/json" \
    -d "{
      \"fields\": {
        \"username\": \"agent$i\",
        \"brandId\": \"brand123\"
      }
    }" -k
done
```

### 3. Run Parallel Tests

```bash
# Simulate 5 parallel tests
for i in {1..5}; do
  (curl -X GET https://localhost:5001/api/entities/Agent/next -k &)
done
```

Each test gets a unique agent!

### 4. Reset After Tests

```bash
curl -X POST https://localhost:5001/api/entities/Agent/reset-all -k
```

---

## Summary

The **ExcludeOnFetch** feature provides:

? **Thread-Safe Test Data** - No conflicts in parallel tests  
? **Atomic Operations** - Race condition free  
? **Easy Cleanup** - Reset with single API call  
? **Flexible** - Enable per entity type  
? **Scalable** - Works with any number of parallel tests  

**Perfect for parallel test execution in CI/CD pipelines!**
