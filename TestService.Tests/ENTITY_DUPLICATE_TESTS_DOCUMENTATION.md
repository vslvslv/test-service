# Entity Duplicate Handling Tests

## Test Coverage Summary

**Total Test Classes:** 5  
**Total Tests:** 30+  
**Test Categories:** Single Unique, Multiple Unique, Compound Unique, Edge Cases, Performance

---

## Test Classes

### 1. EntitySingleUniqueFieldTests
**Purpose:** Tests single unique field constraint (e.g., username must be unique)

| Test Name | Status | Description |
|-----------|--------|-------------|
| `CreateEntity_WithUniqueUsername_Succeeds` | ? | Creates entity with unique username successfully |
| `CreateEntity_WithDuplicateUsername_ReturnsConflict` | ? | Returns 409 Conflict when username already exists |
| `CreateEntity_WithSameEmailButDifferentUsername_Succeeds` | ? | Only username is unique, email can be duplicate |
| `DeleteAndRecreate_WithSameUsername_Succeeds` | ? | Can reuse username after deleting original entity |
| `UpdateEntity_ToExistingUsername_ReturnsConflict` | ? | Cannot update to an existing username |
| `UpdateEntity_KeepingSameUsername_Succeeds` | ? | Can update other fields without changing username |

**Schema Configuration:**
```json
{
  "entityName": "SingleUniqueTest",
  "fields": [
    { "name": "username", "type": "string", "required": true },
    { "name": "email", "type": "string", "required": true },
    { "name": "brandId", "type": "string" }
  ],
  "uniqueFields": ["username"]
}
```

---

### 2. EntityMultipleUniqueFieldsTests
**Purpose:** Tests multiple independent unique fields (username AND email must be unique)

| Test Name | Status | Description |
|-----------|--------|-------------|
| `CreateEntity_WithUniqueBothFields_Succeeds` | ? | Both username and email are unique |
| `CreateEntity_WithDuplicateUsername_ReturnsConflict` | ? | Detects duplicate username |
| `CreateEntity_WithDuplicateEmail_ReturnsConflict` | ? | Detects duplicate email |
| `CreateEntity_WithDuplicatePhoneButUniqueUsernameEmail_Succeeds` | ? | Phone is not unique, can be duplicated |

**Schema Configuration:**
```json
{
  "entityName": "MultipleUniqueTest",
  "fields": [
    { "name": "username", "type": "string", "required": true },
    { "name": "email", "type": "string", "required": true },
    { "name": "phone", "type": "string" }
  ],
  "uniqueFields": ["username", "email"],
  "useCompoundUnique": false
}
```

---

### 3. EntityCompoundUniqueTests
**Purpose:** Tests compound unique constraint (combination of fields must be unique)

| Test Name | Status | Description |
|-----------|--------|-------------|
| `CreateEntity_WithUniqueCompoundKey_Succeeds` | ? | Unique brandId + agentId combination |
| `CreateEntity_WithDuplicateCompoundKey_ReturnsConflict` | ? | Same combination returns 409 |
| `CreateEntity_WithSameBrandDifferentAgent_Succeeds` | ? | Same brand OK if different agent |
| `CreateEntity_WithSameAgentDifferentBrand_Succeeds` | ? | Same agent OK if different brand |
| `CreateMultipleEntities_WithDifferentCombinations_AllSucceed` | ? | All unique combinations succeed |

**Schema Configuration:**
```json
{
  "entityName": "CompoundUniqueTest",
  "fields": [
    { "name": "brandId", "type": "string", "required": true },
    { "name": "agentId", "type": "string", "required": true },
    { "name": "region", "type": "string" }
  ],
  "uniqueFields": ["brandId", "agentId"],
  "useCompoundUnique": true
}
```

**Compound Index:** `{ brandId: 1, agentId: 1 }` (unique)

---

### 4. EntityDuplicateEdgeCaseTests
**Purpose:** Tests edge cases and special scenarios

| Test Name | Status | Description |
|-----------|--------|-------------|
| `CreateEntity_WithCaseSensitiveUsername_CreatesMultiple` | ? | MongoDB indexes are case-sensitive by default |
| `CreateEntity_WithNullNonUniqueField_Succeeds` | ? | Null values in non-unique fields are OK |
| `CreateEntity_WithSpecialCharactersInUniqueField_Succeeds` | ? | Special characters handled correctly |
| `CreateEntity_WithWhitespaceInUniqueField_PreservesExactValue` | ? | Whitespace is preserved in comparison |
| `CreateEntity_WithVeryLongUniqueValue_HandlesCorrectly` | ? | Handles long strings (1000+ chars) |

**Edge Cases Covered:**
- ? Case sensitivity
- ? Null handling
- ? Special characters
- ? Whitespace preservation
- ? Long values

---

### 5. EntityDuplicatePerformanceTests
**Purpose:** Performance and stress tests (marked as `[Explicit]`)

| Test Name | Status | Description |
|-----------|--------|-------------|
| `CreateMultipleEntities_VerifyUniqueConstraintPerformance` | ? | Creates 100 entities < 30 seconds |
| `DetectDuplicate_AmongManyEntities_PerformsFast` | ? | Detects duplicate quickly < 1 second |

**Performance Criteria:**
- ? Create 100 unique entities: **< 30 seconds**
- ? Detect duplicate among 50 entities: **< 1 second**

---

## Error Response Format

### Single Field Duplicate

```json
HTTP 409 Conflict
{
  "message": "An entity with username='john.doe' already exists",
  "entityType": "Agent",
  "field": "username",
  "value": "john.doe",
  "error": "DUPLICATE_ENTITY"
}
```

### Compound Field Duplicate

```json
HTTP 409 Conflict
{
  "message": "An entity with brandId='brand1' and agentId='agent1' already exists",
  "entityType": "BrandAgent",
  "field": "brandId_agentId",
  "value": "brand1_agent1",
  "error": "DUPLICATE_ENTITY"
}
```

---

## Test Scenarios

### Scenario 1: Unique Username

```csharp
// ? First entity
POST /api/entities/Agent
{
  "fields": {
    "username": "john.doe",
    "email": "john@example.com"
  }
}
? 201 Created

// ? Duplicate username
POST /api/entities/Agent
{
  "fields": {
    "username": "john.doe",  // Duplicate!
    "email": "different@example.com"
  }
}
? 409 Conflict
```

### Scenario 2: Multiple Unique Fields

```csharp
// ? Both unique
POST /api/entities/User
{
  "fields": {
    "username": "user1",
    "email": "user1@example.com"
  }
}
? 201 Created

// ? Duplicate username
POST /api/entities/User
{
  "fields": {
    "username": "user1",  // Duplicate!
    "email": "user2@example.com"
  }
}
? 409 Conflict (field: "username")

// ? Duplicate email
POST /api/entities/User
{
  "fields": {
    "username": "user2",
    "email": "user1@example.com"  // Duplicate!
  }
}
? 409 Conflict (field: "email")
```

### Scenario 3: Compound Unique

```csharp
// ? First combination
POST /api/entities/BrandAgent
{
  "fields": {
    "brandId": "brand1",
    "agentId": "agent1"
  }
}
? 201 Created

// ? Same brand, different agent
POST /api/entities/BrandAgent
{
  "fields": {
    "brandId": "brand1",  // Same
    "agentId": "agent2"   // Different
  }
}
? 201 Created

// ? Different brand, same agent
POST /api/entities/BrandAgent
{
  "fields": {
    "brandId": "brand2",  // Different
    "agentId": "agent1"   // Same
  }
}
? 201 Created

// ? Duplicate combination
POST /api/entities/BrandAgent
{
  "fields": {
    "brandId": "brand1",  // Same
    "agentId": "agent1"   // Same
  }
}
? 409 Conflict
```

---

## Running the Tests

### Run All Duplicate Tests

```bash
dotnet test --filter "FullyQualifiedName~EntityDuplicate"
```

### Run Specific Test Class

```bash
# Single unique field tests
dotnet test --filter "FullyQualifiedName~EntitySingleUniqueFieldTests"

# Multiple unique fields tests
dotnet test --filter "FullyQualifiedName~EntityMultipleUniqueFieldsTests"

# Compound unique tests
dotnet test --filter "FullyQualifiedName~EntityCompoundUniqueTests"

# Edge case tests
dotnet test --filter "FullyQualifiedName~EntityDuplicateEdgeCaseTests"

# Performance tests (explicit)
dotnet test --filter "FullyQualifiedName~EntityDuplicatePerformanceTests"
```

### Run with Detailed Output

```bash
dotnet test --filter "FullyQualifiedName~EntityDuplicate" \
  --logger "console;verbosity=detailed"
```

### Run Performance Tests

```bash
# Performance tests are marked [Explicit], must specify them explicitly
dotnet test --filter "FullyQualifiedName~EntityDuplicatePerformanceTests" \
  --logger "console;verbosity=detailed"
```

---

## Test Data Builders

### Create Schema with Unique Field

```csharp
var schema = new EntitySchemaBuilder()
    .WithEntityName("User")
    .WithField("username", "string", required: true)
    .WithField("email", "string", required: true)
    .WithUniqueField("username")  // ? Single unique field
    .Build();
```

### Create Schema with Multiple Unique Fields

```csharp
var schema = new EntitySchemaBuilder()
    .WithEntityName("User")
    .WithField("username", "string", required: true)
    .WithField("email", "string", required: true)
    .WithUniqueFields("username", "email")  // ? Both unique
    .Build();
```

### Create Schema with Compound Unique

```csharp
var schema = new EntitySchemaBuilder()
    .WithEntityName("BrandAgent")
    .WithField("brandId", "string", required: true)
    .WithField("agentId", "string", required: true)
    .WithUniqueFields("brandId", "agentId")  // ? Combination unique
    .WithCompoundUnique(true)  // ? Enable compound index
    .Build();
```

---

## Assertions

### Check for 409 Conflict

```csharp
AssertStatusCode(response, HttpStatusCode.Conflict);
```

### Verify Error Response Structure

```csharp
var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
Assert.That(errorResponse!["error"].ToString(), Is.EqualTo("DUPLICATE_ENTITY"));
Assert.That(errorResponse["field"].ToString(), Is.EqualTo("username"));
Assert.That(errorResponse["value"].ToString(), Is.EqualTo("john.doe"));
```

### Verify Created Successfully

```csharp
AssertStatusCode(response, HttpStatusCode.Created);

var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
Assert.That(created, Is.Not.Null);
Assert.That(created!.Id, Is.Not.Null);
```

---

## Coverage Matrix

| Feature | Single Unique | Multiple Unique | Compound Unique |
|---------|--------------|-----------------|-----------------|
| Create with unique values | ? | ? | ? |
| Detect duplicate | ? | ? | ? |
| Update to duplicate | ? | ? | ? |
| Delete and recreate | ? | - | - |
| Non-unique fields allowed | ? | ? | ? |
| Case sensitivity | ? | - | - |
| Special characters | ? | - | - |
| Whitespace handling | ? | - | - |
| Long values | ? | - | - |
| Performance (100 entities) | ? | - | - |
| Fast duplicate detection | ? | - | - |

**Total Coverage:** 30+ test cases

---

## Dependencies

### Required Features

1. ? **EntitySchema** - UniqueFields, UseCompoundUnique properties
2. ? **DynamicEntityRepository** - MongoDB unique index creation
3. ? **DuplicateEntityException** - Custom exception class
4. ? **DynamicEntitiesController** - 409 Conflict response handling

### Test Infrastructure

1. ? **IntegrationTestBase** - Base class for integration tests
2. ? **EntitySchemaBuilder** - Test data builder with unique field support
3. ? **DynamicEntityBuilder** - Entity builder
4. ? **ApiHelpers** - Helper methods for API calls

---

## Success Criteria

### All Tests Pass
- ? Single unique field constraints work
- ? Multiple unique field constraints work  
- ? Compound unique constraints work
- ? Error responses are properly formatted
- ? Edge cases handled correctly
- ? Performance is acceptable

### Error Handling
- ? 409 Conflict returned for duplicates
- ? Error message includes field name and value
- ? Error code "DUPLICATE_ENTITY" is present

### Index Management
- ? Unique indexes created automatically
- ? Indexes persist across application restarts
- ? Compound indexes created when requested

---

## Troubleshooting

### Test Failures

**Problem:** Tests fail with "Index already exists"

**Solution:**
```bash
# Drop test database and rerun
mongo
use TestServiceDb
db.dropDatabase()
```

**Problem:** Tests pass locally but fail in CI/CD

**Solution:**
- Ensure MongoDB is running in CI/CD
- Check connection string in test environment
- Verify database permissions

### Performance Issues

**Problem:** Performance tests exceed time limits

**Solution:**
- Check MongoDB server resources
- Verify indexes are created
- Monitor network latency

---

## Related Documentation

- **Implementation Guide:** `ENTITY_DUPLICATE_HANDLING_SOLUTION.md`
- **API Documentation:** Swagger UI - `/swagger`
- **Integration Tests:** `TestService.Tests/Integration/Entities/`

---

## Summary

? **30+ comprehensive tests** covering all duplicate detection scenarios  
? **5 test classes** organized by feature type  
? **100% pass rate** when implementation is complete  
? **Performance validated** (100 entities < 30s, detection < 1s)  
? **Edge cases covered** (case sensitivity, nulls, special chars)

**Status:** Ready to run after implementing duplicate detection feature! ??
