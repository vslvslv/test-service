# Duplicate Entity Tests - Quick Reference

## ?? Test Summary

| Category | Tests | Description |
|----------|-------|-------------|
| **Single Unique Field** | 6 | Username must be unique |
| **Multiple Unique Fields** | 4 | Username AND email must be unique |
| **Compound Unique** | 5 | BrandId + AgentId combination must be unique |
| **Edge Cases** | 5 | Case sensitivity, nulls, special chars, whitespace |
| **Performance** | 2 | 100 entities creation, fast duplicate detection |
| **TOTAL** | **22** | Comprehensive duplicate detection coverage |

---

## ?? Quick Start

### 1. Run All Duplicate Tests

```bash
cd TestService.Tests
dotnet test --filter "FullyQualifiedName~EntityDuplicate"
```

### 2. Expected Output

```
Test Run Successful
===================
Total tests: 22
     Passed: 22 (100%)
     Failed: 0
   Skipped: 2 (Performance tests)
```

---

## ?? Test Files Created

1. ? **EntityDuplicateTests.cs** - Main test file with 5 test classes
2. ? **TestDataBuilders.cs** - Updated with `WithUniqueField()` support
3. ? **ENTITY_DUPLICATE_TESTS_DOCUMENTATION.md** - Detailed documentation

---

## ?? What Was Added

### TestDataBuilders.cs - New Methods

```csharp
// Single unique field
.WithUniqueField("username")

// Multiple unique fields
.WithUniqueFields("username", "email")

// Compound unique
.WithUniqueFields("brandId", "agentId")
.WithCompoundUnique(true)
```

### Example Test

```csharp
[Test]
public async Task CreateEntity_WithDuplicateUsername_ReturnsConflict()
{
    // Create first entity
    var entity1 = new DynamicEntityBuilder()
        .WithField("username", "john.doe")
        .WithField("email", "john@example.com")
        .Build();
    await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

    // Try duplicate
    var entity2 = new DynamicEntityBuilder()
        .WithField("username", "john.doe")  // Duplicate!
        .WithField("email", "different@example.com")
        .Build();

    var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

    // Assert
    AssertStatusCode(response, HttpStatusCode.Conflict);
    
    var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    Assert.That(errorResponse!["error"].ToString(), Is.EqualTo("DUPLICATE_ENTITY"));
    Assert.That(errorResponse["field"].ToString(), Is.EqualTo("username"));
    Assert.That(errorResponse["value"].ToString(), Is.EqualTo("john.doe"));
}
```

---

## ? Test Scenarios Covered

### 1. Single Unique Field
- ? Create with unique value ? **201 Created**
- ? Create with duplicate ? **409 Conflict**
- ? Update to duplicate ? **409 Conflict**
- ? Delete and recreate ? **201 Created**
- ? Update other fields, keep unique field ? **204 No Content**

### 2. Multiple Unique Fields
- ? Both fields unique ? **201 Created**
- ? Duplicate field 1 ? **409 Conflict (field: "username")**
- ? Duplicate field 2 ? **409 Conflict (field: "email")**
- ? Non-unique field duplicate ? **201 Created**

### 3. Compound Unique
- ? Unique combination ? **201 Created**
- ? Duplicate combination ? **409 Conflict**
- ? Same field1, different field2 ? **201 Created**
- ? Different field1, same field2 ? **201 Created**
- ? Multiple different combinations ? **All 201 Created**

### 4. Edge Cases
- ? Case sensitivity (`"User"` vs `"user"`) ? **Both created**
- ? Null in non-unique field ? **201 Created**
- ? Special characters (`user+test@example.com`) ? **201 Created**
- ? Whitespace preserved (`"  user  "`) ? **Exact match required**
- ? Very long values (1000+ chars) ? **201 Created**

### 5. Performance
- ? Create 100 unique entities ? **< 30 seconds**
- ? Detect duplicate among 50 ? **< 1 second**

---

## ?? Expected Error Response

```json
HTTP/1.1 409 Conflict
Content-Type: application/json

{
  "message": "An entity with username='john.doe' already exists",
  "entityType": "Agent",
  "field": "username",
  "value": "john.doe",
  "error": "DUPLICATE_ENTITY"
}
```

---

## ?? Run Specific Test Categories

```bash
# Single unique field tests
dotnet test --filter "EntitySingleUniqueFieldTests"

# Multiple unique fields tests
dotnet test --filter "EntityMultipleUniqueFieldsTests"

# Compound unique tests
dotnet test --filter "EntityCompoundUniqueTests"

# Edge case tests
dotnet test --filter "EntityDuplicateEdgeCaseTests"

# Performance tests (explicit)
dotnet test --filter "EntityDuplicatePerformanceTests"
```

---

## ?? Prerequisites

### Before Running Tests

1. **MongoDB must be running**
   ```bash
   docker ps | grep testservice-mongodb
   ```

2. **API implementation complete**
   - `EntitySchema` has `UniqueFields` and `UseCompoundUnique` properties
   - `DynamicEntityRepository` creates unique indexes
   - `DuplicateEntityException` class exists
   - Controller returns 409 Conflict with proper error format

3. **Test infrastructure ready**
   - `IntegrationTestBase` works
   - `EntitySchemaBuilder` updated with unique field support
   - API connection configured

---

## ?? Troubleshooting

### Tests Fail to Compile

**Error:** `'EntitySchema' does not contain a definition for 'UniqueFields'`

**Fix:** Implement the duplicate detection feature first (see `ENTITY_DUPLICATE_HANDLING_SOLUTION.md`)

### Tests Fail at Runtime

**Error:** `MongoDB connection failed`

**Fix:**
```bash
cd infrastructure
./start.sh  # or start.bat on Windows
```

**Error:** `Index already exists`

**Fix:**
```javascript
// MongoDB shell
use TestServiceDb
db.getSiblingDB('TestServiceDb').getCollectionNames().forEach(function(c) {
    if (c.startsWith('Dynamic_')) {
        db.getSiblingDB('TestServiceDb')[c].dropIndexes();
    }
});
```

---

## ?? Documentation

- **Full Test Documentation:** `ENTITY_DUPLICATE_TESTS_DOCUMENTATION.md`
- **Implementation Guide:** `ENTITY_DUPLICATE_HANDLING_SOLUTION.md`
- **Test File:** `TestService.Tests/Integration/Entities/EntityDuplicateTests.cs`
- **Test Builders:** `TestService.Tests/Infrastructure/TestDataBuilders.cs`

---

## ? Next Steps

1. **Implement duplicate detection feature** (if not done yet)
   - Follow `ENTITY_DUPLICATE_HANDLING_SOLUTION.md`
   - Add `UniqueFields` to `EntitySchema`
   - Create `DuplicateEntityException`
   - Update repository and controller

2. **Run tests to verify implementation**
   ```bash
   dotnet test --filter "EntityDuplicate"
   ```

3. **Check results**
   - All 22 tests should pass
   - Error responses should match expected format
   - Performance tests should meet criteria

4. **Update frontend** (optional)
   - Display duplicate error messages
   - Highlight duplicate fields in forms

---

## ?? Success Criteria

? All 22 tests pass  
? Error response format matches specification  
? Unique indexes created automatically  
? Duplicate detection is fast (< 1 second)  
? Edge cases handled correctly  

---

**Ready to test!** ??

Run: `dotnet test --filter "EntityDuplicate"` to verify your implementation.
