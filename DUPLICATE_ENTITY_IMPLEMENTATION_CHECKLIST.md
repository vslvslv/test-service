# Duplicate Entity Tests - Implementation Checklist

## ?? Current Status

**Tests Created:** ? 22 comprehensive tests  
**Tests Compilable:** ? No - waiting for feature implementation  
**Tests Runnable:** ? No - dependencies not implemented yet

---

## ?? Implementation Required

Before the tests can run, you need to implement the duplicate detection feature:

### 1. Update Entity Schema Model ?

**File:** `TestService.Api/Models/EntitySchema.cs`

**Add:**
```csharp
[BsonElement("uniqueFields")]
public List<string> UniqueFields { get; set; } = new();

[BsonElement("useCompoundUnique")]
public bool UseCompoundUnique { get; set; } = false;
```

**Status:** ? Not implemented

---

### 2. Create Custom Exception ?

**File:** `TestService.Api/Exceptions/DuplicateEntityException.cs` (NEW)

**Add:**
```csharp
namespace TestService.Api.Exceptions;

public class DuplicateEntityException : Exception
{
    public string EntityType { get; }
    public string FieldName { get; }
    public string FieldValue { get; }

    public DuplicateEntityException(
        string entityType, 
        string fieldName, 
        string fieldValue,
        string message) : base(message)
    {
        EntityType = entityType;
        FieldName = fieldName;
        FieldValue = fieldValue;
    }
}
```

**Status:** ? Not implemented

---

### 3. Update Repository ?

**File:** `TestService.Api/Services/DynamicEntityRepository.cs`

**Add:**
- `EnsureUniqueIndexesAsync()` method
- MongoDB unique index creation
- Duplicate key exception handling in `CreateAsync()`
- Duplicate key exception handling in `UpdateAsync()`

**Status:** ? Not implemented

---

### 4. Update Service Interface ?

**File:** `TestService.Api/Services/DynamicEntityService.cs`

**Add:**
```csharp
Task EnsureUniqueIndexesAsync(string entityType, EntitySchema schema);
```

**Status:** ? Not implemented

---

### 5. Update Controller ?

**File:** `TestService.Api/Controllers/DynamicEntitiesController.cs`

**Update `Create()` method:**
- Call `EnsureUniqueIndexesAsync()`
- Catch `DuplicateEntityException`
- Return 409 Conflict with proper error structure

**Status:** ? Not implemented

---

### 6. Update Frontend Types ?

**File:** `testservice-web/src/types/index.ts`

**Add to Schema interface:**
```typescript
export interface Schema {
  entityName: string;
  fields: SchemaField[];
  filterableFields?: string[];
  uniqueFields?: string[];
  useCompoundUnique?: boolean;
  excludeOnFetch: boolean;
  createdAt?: string;
}
```

**Status:** ? Not implemented

---

## ?? Implementation Order

Follow this order for smooth implementation:

1. ? **Tests Created** (DONE)
   - EntityDuplicateTests.cs
   - Updated TestDataBuilders.cs
   - Documentation files

2. ? **Backend Models**
   - Update EntitySchema.cs
   - Create DuplicateEntityException.cs

3. ? **Repository Layer**
   - Update DynamicEntityRepository.cs
   - Add index management
   - Add exception handling

4. ? **Service Layer**
   - Update IDynamicEntityService interface
   - Implement EnsureUniqueIndexesAsync()

5. ? **Controller Layer**
   - Update DynamicEntitiesController.cs
   - Add 409 Conflict handling

6. ? **Frontend Types**
   - Update Schema interface
   - Update EntityCreateDialog error handling

7. ? **Verify Tests**
   - Run: `dotnet test --filter "EntityDuplicate"`
   - All 22 tests should pass

---

## ?? Quick Start After Implementation

```bash
# 1. Verify API compiles
cd TestService.Api
dotnet build

# 2. Verify tests compile
cd ../TestService.Tests
dotnet build

# 3. Run duplicate tests
dotnet test --filter "EntityDuplicate"

# Expected output:
# Test Run Successful
# Total tests: 22
#      Passed: 22 (100%)
#      Failed: 0
```

---

## ?? Files Created

### Test Files ?

1. **TestService.Tests/Integration/Entities/EntityDuplicateTests.cs**
   - 5 test classes
   - 22 test methods
   - Comprehensive coverage

2. **TestService.Tests/Infrastructure/TestDataBuilders.cs** (UPDATED)
   - Added `WithUniqueField()`
   - Added `WithUniqueFields()`
   - Added `WithCompoundUnique()`

### Documentation Files ?

1. **ENTITY_DUPLICATE_HANDLING_SOLUTION.md**
   - Complete implementation guide
   - Code examples
   - API examples
   - Usage patterns

2. **TestService.Tests/ENTITY_DUPLICATE_TESTS_DOCUMENTATION.md**
   - Test coverage details
   - Test scenarios
   - Running tests guide
   - Assertions guide

3. **DUPLICATE_TESTS_QUICK_START.md**
   - Quick reference
   - Command cheat sheet
   - Troubleshooting guide

4. **DUPLICATE_ENTITY_IMPLEMENTATION_CHECKLIST.md** (THIS FILE)
   - Implementation order
   - Status tracking
   - Verification steps

---

## ?? Verification Steps

### After Each Implementation Step

1. **After updating EntitySchema:**
   ```bash
   dotnet build TestService.Api
   dotnet build TestService.Tests  # Should compile now
   ```

2. **After creating DuplicateEntityException:**
   ```bash
   dotnet build TestService.Api
   ```

3. **After updating Repository:**
   ```bash
   dotnet build TestService.Api
   # Test index creation manually
   ```

4. **After updating Controller:**
   ```bash
   dotnet build TestService.Api
   # Run API locally and test duplicate detection
   ```

5. **After everything:**
   ```bash
   dotnet test --filter "EntityDuplicate"
   ```

---

## ?? Success Criteria

### Compilation
- ? TestService.Api builds without errors
- ? TestService.Tests builds without errors

### Tests
- ? All 22 duplicate tests pass
- ? No test failures
- ? Performance tests meet criteria

### Runtime
- ? MongoDB unique indexes created automatically
- ? Duplicate detection works in API
- ? 409 Conflict returned with correct format
- ? Error messages are user-friendly

### Frontend (Optional)
- ? Duplicate errors displayed in UI
- ? Field-level errors highlighted
- ? User-friendly messages shown

---

## ?? Reference Documentation

| Document | Purpose |
|----------|---------|
| `ENTITY_DUPLICATE_HANDLING_SOLUTION.md` | Complete implementation guide with code |
| `ENTITY_DUPLICATE_TESTS_DOCUMENTATION.md` | Test coverage and scenarios |
| `DUPLICATE_TESTS_QUICK_START.md` | Quick reference and commands |
| `DUPLICATE_ENTITY_IMPLEMENTATION_CHECKLIST.md` | This file - tracking progress |

---

## ?? Known Issues

### Compilation Errors (Expected)

```
CS0117: 'EntitySchema' does not contain a definition for 'UniqueFields'
CS0117: 'EntitySchema' does not contain a definition for 'UseCompoundUnique'
```

**Status:** ? Normal - will be fixed when EntitySchema is updated

**Fix:** Follow implementation guide in `ENTITY_DUPLICATE_HANDLING_SOLUTION.md`

---

## ?? Need Help?

### Implementation Help
- Read: `ENTITY_DUPLICATE_HANDLING_SOLUTION.md` (complete guide)
- Check: MongoDB documentation for unique indexes
- Test: Use Postman/curl to verify API behavior

### Test Help
- Read: `ENTITY_DUPLICATE_TESTS_DOCUMENTATION.md`
- Run: `dotnet test --filter "EntitySingleUniqueFieldTests" -v detailed`
- Debug: Use VS Code debugger with test runner

---

## ? Current Progress

**Phase 1: Tests** ? COMPLETE
- [x] Create test file with 22 tests
- [x] Update TestDataBuilders
- [x] Create documentation
- [x] Create quick start guide

**Phase 2: Backend Implementation** ? WAITING
- [ ] Update EntitySchema model
- [ ] Create DuplicateEntityException
- [ ] Update DynamicEntityRepository
- [ ] Update DynamicEntityService
- [ ] Update DynamicEntitiesController

**Phase 3: Frontend** ? WAITING
- [ ] Update TypeScript types
- [ ] Update EntityCreateDialog
- [ ] Display error messages
- [ ] Highlight duplicate fields

**Phase 4: Verification** ? WAITING
- [ ] All tests pass
- [ ] API manual testing complete
- [ ] Frontend testing complete
- [ ] Documentation updated

---

## ?? What's Next?

1. **Start Implementation:**
   - Open `ENTITY_DUPLICATE_HANDLING_SOLUTION.md`
   - Follow step-by-step guide
   - Implement backend changes

2. **Verify Each Step:**
   - Build after each change
   - Fix any compilation errors
   - Test functionality manually

3. **Run Tests:**
   - After implementation complete
   - Run: `dotnet test --filter "EntityDuplicate"`
   - All 22 tests should pass

4. **Update UI (Optional):**
   - Update TypeScript types
   - Enhance error messages
   - Test in browser

---

**Status:** ? Ready to implement!  
**Next Step:** Update `EntitySchema.cs` with `UniqueFields` and `UseCompoundUnique` properties

**Good luck! ??**
