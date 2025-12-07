# Test Service - Test Suite Documentation

## Overview

The Test Service test suite is a comprehensive collection of integration, unit, and end-to-end tests organized by functionality and purpose.

---

## Test Structure

```
TestService.Tests/
??? Infrastructure/                    # Base classes and utilities
?   ??? IntegrationTestBase.cs        - Base class for all integration tests
?   ??? TestDataBuilders.cs           - Builder pattern for test data
?   ??? ApiHelpers.cs                 - Helper methods for API operations
?
??? Integration/                       # Integration tests
?   ??? Schemas/
?   ?   ??? SchemaTests.cs            - Schema CRUD operations
?   ??? Entities/
?       ??? EntityCrudTests.cs        - Entity CRUD operations
?       ??? ParallelExecutionTests.cs - ExcludeOnFetch feature tests
?
??? EndToEnd/                          # End-to-end scenario tests
?   ??? CompleteWorkflowTests.cs      - Full workflow scenarios
?
??? Legacy/                            # Legacy API tests
?   ??? AgentApiTests.cs              - Agent API (backwards compatibility)
?   ??? TestDataApiTests.cs           - TestData API (backwards compatibility)
?
??? GlobalUsings.cs                    # Global using directives
??? TestService.Tests.csproj           # Project file
```

---

## Test Categories

### Infrastructure Tests
**Location**: `Infrastructure/`
- **IntegrationTestBase**: Base class providing common setup, teardown, and utility methods
- **TestDataBuilders**: Fluent builders for creating test objects (EntitySchema, DynamicEntity)
- **ApiHelpers**: Reusable methods for common API operations

### Schema Tests
**Location**: `Integration/Schemas/SchemaTests.cs`

#### Positive Scenarios (30 tests)
- ? Create schema with valid data
- ? Create schema with all field types
- ? Create schema with exclude on fetch
- ? Create schema with multiple required fields
- ? Get all schemas
- ? Get schema by name
- ? Update schema
- ? Delete schema

#### Negative Scenarios (6 tests)
- ? Create schema with empty name
- ? Create schema with duplicate name
- ? Get non-existent schema
- ? Update non-existent schema
- ? Invalid field configurations

### Entity CRUD Tests
**Location**: `Integration/Entities/EntityCrudTests.cs`

#### Creation Tests (45+ tests)
**Positive:**
- ? Create with all fields
- ? Create with only required fields
- ? Create multiple entities
- ? Create with special characters
- ? Create with null optional fields

**Negative:**
- ? Missing required fields
- ? Non-existent schema
- ? Empty fields
- ? Null required fields

#### Retrieval Tests (20+ tests)
- ? Get all entities
- ? Get by ID
- ? Filter by field
- ? Filter with no matches
- ? Invalid ID
- ? Malformed ID
- ? Non-filterable field

#### Update Tests (15+ tests)
- ? Update with valid changes
- ? Add new fields
- ? Remove optional fields
- ? Invalid ID
- ? Remove required fields

#### Deletion Tests (10+ tests)
- ? Delete with valid ID
- ? Delete then recreate
- ? Invalid ID
- ? Delete twice

### Parallel Execution Tests
**Location**: `Integration/Entities/ParallelExecutionTests.cs`

#### Functional Tests (30+ tests)
- ? Get next available entity
- ? Multiple calls return different entities
- ? Consumed entities excluded from queries
- ? Reset consumed entity
- ? Reset all consumed entities
- ? Atomic operations prevent conflicts
- ? No available entities
- ? Schema without exclude on fetch

#### Stress Tests (Explicit)
- ?? 100 parallel requests
- ?? Large dataset operations

### End-to-End Tests
**Location**: `EndToEnd/CompleteWorkflowTests.cs`

#### Complete Workflows (25+ tests)
1. **Product Management Workflow**
   - Create schema
   - Create multiple products
   - Filter by category
   - Update prices
   - Delete products
   - Delete schema

2. **Parallel Test Execution Workflow**
   - Create test user pool
   - Simulate parallel tests
   - Each test gets unique user
   - Reset for reuse

3. **Multi-Type Data Management**
   - Create related schemas (Customer, Product, Order)
   - Create entities with relationships
   - Query across types
   - Cleanup

4. **Schema Evolution**
   - Create V1 schema
   - Add entities
   - Update schema to V2
   - Migrate existing data
   - Verify migration

5. **Bulk Operations**
   - Create 50+ entities
   - Bulk filter
   - Bulk update
   - Bulk delete

---

## Running Tests

### Run All Tests
```bash
cd TestService.Tests
dotnet test
```

### Run Specific Category
```bash
# Schema tests
dotnet test --filter "FullyQualifiedName~SchemaTests"

# Entity CRUD tests
dotnet test --filter "FullyQualifiedName~EntityCrudTests"

# Parallel execution tests
dotnet test --filter "FullyQualifiedName~ParallelExecutionTests"

# End-to-end tests
dotnet test --filter "FullyQualifiedName~CompleteWorkflowTests"

# Legacy tests
dotnet test --filter "FullyQualifiedName~Legacy"
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~SchemaCreationPositiveTests"
```

### Run with Detailed Output
```bash
dotnet test --verbosity normal
```

### Run with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Skip Explicit Tests
```bash
dotnet test --filter "Category!=Explicit"
```

---

## Test Data Builders

### EntitySchemaBuilder
Creates test schemas with fluent API:

```csharp
var schema = new EntitySchemaBuilder()
    .WithEntityName("Product")
    .WithField("name", "string", required: true)
    .WithField("price", "number", required: true)
    .WithFilterableFields("name", "category")
    .WithExcludeOnFetch(true)
    .Build();
```

**Prebuilt Schemas:**
- `CreateDefaultAgentSchema()` - Full Agent schema
- `CreateMinimalSchema()` - Minimal test schema

### DynamicEntityBuilder
Creates test entities with fluent API:

```csharp
var entity = new DynamicEntityBuilder()
    .WithField("name", "Test Product")
    .WithField("price", 99.99)
    .WithField("category", "Electronics")
    .Build();
```

**Prebuilt Entities:**
- `CreateDefaultAgent()` - Default Agent entity
- `CreateMinimalEntity()` - Minimal test entity

---

## API Helpers

Reusable methods for common operations:

```csharp
// Create schema
await ApiHelpers.CreateSchemaAsync(client, schema);

// Create entity
var entity = await ApiHelpers.CreateEntityAsync(client, "Product", product);

// Create multiple
var entities = await ApiHelpers.CreateEntitiesAsync(client, "Product", product1, product2);

// Get all
var all = await ApiHelpers.GetAllEntitiesAsync(client, "Product");

// Get by ID
var entity = await ApiHelpers.GetEntityByIdAsync(client, "Product", id);

// Update
await ApiHelpers.UpdateEntityAsync(client, "Product", id, updated);

// Delete
await ApiHelpers.DeleteEntityAsync(client, "Product", id);

// Reset consumed
await ApiHelpers.ResetAllConsumedAsync(client, "Product");
```

---

## Test Coverage

### Current Statistics
- **Total Tests**: 150+
- **Integration Tests**: 100+
- **End-to-End Tests**: 25+
- **Legacy Tests**: 20+
- **Positive Scenarios**: ~70%
- **Negative Scenarios**: ~20%
- **Stress Tests**: ~10%

### Coverage by Feature

| Feature | Tests | Positive | Negative | E2E |
|---------|-------|----------|----------|-----|
| Schema CRUD | 30 | 20 | 10 | 5 |
| Entity CRUD | 45 | 30 | 15 | 8 |
| Filtering | 20 | 15 | 5 | 3 |
| Parallel Execution | 30 | 25 | 5 | 5 |
| Bulk Operations | 10 | 8 | 2 | 4 |
| Schema Evolution | 5 | 5 | 0 | 1 |

---

## Best Practices

### Test Organization
1. **Use IntegrationTestBase** for all integration tests
2. **One test class per feature** area
3. **Descriptive test names** (Given_When_Then or Action_Condition_Expected)
4. **Arrange-Act-Assert** pattern
5. **Clean up test data** in teardown methods

### Test Isolation
1. **Use unique names** for each test (CreateUniqueName, CreateUniqueId)
2. **Reset consumed entities** in SetUp for parallel execution tests
3. **Delete schemas** after use in E2E tests
4. **Don't depend on test order** (except where explicitly needed)

### Assertions
1. **Use AssertStatusCode** for consistent error messages
2. **Check both success and failure** paths
3. **Verify data integrity** after operations
4. **Test edge cases** (null, empty, special characters)

### Performance
1. **Mark slow tests as Explicit**
2. **Use parallel execution** where possible
3. **Reuse test data** within test class
4. **Clean up efficiently**

---

## Negative Test Scenarios

### HTTP Status Codes Tested
- ? **200 OK** - Successful GET
- ? **201 Created** - Successful POST
- ? **204 No Content** - Successful PUT/DELETE
- ? **400 Bad Request** - Invalid data
- ? **404 Not Found** - Resource not found
- ? **409 Conflict** - Duplicate resource

### Error Conditions
- Missing required fields
- Invalid IDs (malformed, non-existent)
- Duplicate names/keys
- Non-filterable field filtering
- Schema not found
- Empty/null values in required fields
- Operations on deleted resources

---

## Continuous Integration

### Prerequisites
- MongoDB running on `localhost:27017`
- RabbitMQ running on `localhost:5672`
- .NET 10.0 SDK

### CI Pipeline
```yaml
- name: Start Infrastructure
  run: docker compose up -d

- name: Wait for Services
  run: |
    timeout 30 bash -c 'until nc -z localhost 27017; do sleep 1; done'
    timeout 30 bash -c 'until nc -z localhost 5672; do sleep 1; done'

- name: Run Tests
  run: dotnet test --verbosity normal --collect:"XPlat Code Coverage"

- name: Stop Infrastructure
  run: docker compose down
```

---

## Troubleshooting

### Tests Failing
1. **Check infrastructure** is running (`docker ps`)
2. **Check connection strings** in appsettings.json
3. **Run single test** to isolate issue
4. **Check logs** in test output

### Slow Tests
1. Mark as **[Explicit]** if > 5 seconds
2. Consider **test data size**
3. Check for **unnecessary delays**
4. Use **parallel execution**

### Flaky Tests
1. Check for **race conditions**
2. Ensure **test isolation**
3. Add **retry logic** if needed
4. Check **external dependencies**

---

## Future Enhancements

### Planned Tests
- [ ] Performance benchmarks
- [ ] Load testing (1000+ concurrent users)
- [ ] Security testing (injection, XSS)
- [ ] API versioning tests
- [ ] Rate limiting tests
- [ ] Pagination tests (when implemented)
- [ ] Sorting tests (when implemented)
- [ ] Complex query tests
- [ ] Transaction tests

### Test Utilities
- [ ] Test data generator
- [ ] API response validators
- [ ] Performance profiler
- [ ] Test report generator

---

## Contributing

### Adding New Tests
1. **Extend appropriate test class** or create new one
2. **Use builders** for test data
3. **Follow naming conventions**
4. **Add both positive and negative** scenarios
5. **Update this documentation**

### Test Naming Convention
```
[TestMethod]_[Condition]_[ExpectedResult]

Examples:
CreateEntity_WithValidData_ReturnsCreated
GetEntity_WithInvalidId_ReturnsNotFound
UpdateEntity_WithMissingRequiredField_ReturnsBadRequest
```

---

**Test Coverage Goal**: 80%+ code coverage with balanced positive/negative scenarios
**Maintenance**: Review and update tests with each feature addition
**Documentation**: Keep this file in sync with actual tests

---

Last Updated: 2025-01-06
Test Count: 150+
Coverage: ~75%
