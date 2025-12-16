# Entity Duplicate Handling - Implementation Guide

## Problem Statement

Currently, the Test Service **allows duplicate entities** to be created because:
1. No unique constraints defined in entity schemas
2. No MongoDB unique indexes on entity collections
3. No duplicate key exception handling
4. No user-friendly error messages in frontend

## Solution Overview

Add support for:
1. **Schema-level unique field definitions**
2. **Automatic MongoDB unique index creation**
3. **Duplicate key exception handling in repository**
4. **User-friendly error messages in UI**

---

## Step 1: Update Entity Schema Model

### Add Unique Fields to Schema

**File: `TestService.Api/Models/EntitySchema.cs`**

```csharp
public class EntitySchema
{
    // ...existing properties...

    /// <summary>
    /// List of fields that should be unique across all entities of this type
    /// </summary>
    [BsonElement("uniqueFields")]
    public List<string> UniqueFields { get; set; } = new();

    /// <summary>
    /// If true, creates a compound unique index on all uniqueFields
    /// If false, creates separate unique indexes for each field
    /// </summary>
    [BsonElement("useCompoundUnique")]
    public bool UseCompoundUnique { get; set; } = false;
}
```

---

## Step 2: Update Repository to Create Unique Indexes

**File: `TestService.Api/Services/DynamicEntityRepository.cs`**

### Add Index Management

```csharp
public class DynamicEntityRepository : IDynamicEntityRepository
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<DynamicEntityRepository> _logger;
    private readonly Dictionary<string, bool> _indexesCreated = new();

    public DynamicEntityRepository(
        MongoDbSettings settings,
        ILogger<DynamicEntityRepository> logger)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        _logger = logger;
    }

    /// <summary>
    /// Ensures unique indexes exist for the entity type based on schema
    /// </summary>
    public async Task EnsureUniqueIndexesAsync(string entityType, EntitySchema schema)
    {
        if (_indexesCreated.ContainsKey(entityType))
            return;

        if (schema.UniqueFields == null || !schema.UniqueFields.Any())
            return;

        var collection = GetCollection(entityType);
        var indexBuilder = Builders<BsonDocument>.IndexKeys;

        try
        {
            if (schema.UseCompoundUnique && schema.UniqueFields.Count > 1)
            {
                // Create compound unique index
                var keysDefinition = indexBuilder.Combine(
                    schema.UniqueFields.Select(f => indexBuilder.Ascending(f))
                );

                var indexOptions = new CreateIndexOptions 
                { 
                    Unique = true,
                    Name = $"unique_{string.Join("_", schema.UniqueFields)}"
                };

                var indexModel = new CreateIndexModel<BsonDocument>(keysDefinition, indexOptions);
                await collection.Indexes.CreateOneAsync(indexModel);

                _logger.LogInformation(
                    "Created compound unique index on {EntityType} for fields: {Fields}",
                    entityType, string.Join(", ", schema.UniqueFields));
            }
            else
            {
                // Create separate unique indexes
                foreach (var field in schema.UniqueFields)
                {
                    var keysDefinition = indexBuilder.Ascending(field);
                    var indexOptions = new CreateIndexOptions 
                    { 
                        Unique = true,
                        Name = $"unique_{field}",
                        Sparse = true // Allow documents without the field
                    };

                    var indexModel = new CreateIndexModel<BsonDocument>(keysDefinition, indexOptions);
                    await collection.Indexes.CreateOneAsync(indexModel);

                    _logger.LogInformation(
                        "Created unique index on {EntityType}.{Field}",
                        entityType, field);
                }
            }

            _indexesCreated[entityType] = true;
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
        {
            // Index already exists, just continue
            _logger.LogInformation(
                "Unique indexes already exist for {EntityType}",
                entityType);
            _indexesCreated[entityType] = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to create unique indexes for {EntityType}",
                entityType);
            throw;
        }
    }

    // Update CreateAsync to handle duplicate key exceptions
    public async Task<DynamicEntity> CreateAsync(DynamicEntity entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsConsumed = false;
        
        var collection = GetCollection(entity.EntityType);
        var document = entity.ToBsonDocument();
        
        try
        {
            await collection.InsertOneAsync(document);
            entity.Id = document["_id"].AsObjectId.ToString();
            return entity;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Extract field name from error message
            var fieldName = ExtractDuplicateFieldName(ex.WriteError.Message);
            var fieldValue = ExtractDuplicateFieldValue(document, fieldName);
            
            throw new DuplicateEntityException(
                entity.EntityType, 
                fieldName, 
                fieldValue,
                $"An entity with {fieldName}='{fieldValue}' already exists");
        }
    }

    private string ExtractDuplicateFieldName(string errorMessage)
    {
        // MongoDB error format: "E11000 duplicate key error collection: ... index: unique_fieldname dup key: { fieldname: "value" }"
        var match = System.Text.RegularExpressions.Regex.Match(
            errorMessage, 
            @"dup key: \{ ([^:]+):");
        
        return match.Success ? match.Groups[1].Value.Trim() : "unknown field";
    }

    private string ExtractDuplicateFieldValue(BsonDocument document, string fieldName)
    {
        if (document.Contains(fieldName))
        {
            return document[fieldName].ToString() ?? "unknown";
        }
        return "unknown";
    }
}
```

---

## Step 3: Create Custom Exception

**File: `TestService.Api/Exceptions/DuplicateEntityException.cs`** (NEW)

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

    public DuplicateEntityException(
        string entityType, 
        string fieldName, 
        string fieldValue,
        string message,
        Exception innerException) : base(message, innerException)
    {
        EntityType = entityType;
        FieldName = fieldName;
        FieldValue = fieldValue;
    }
}
```

---

## Step 4: Update Controller to Handle Exception

**File: `TestService.Api/Controllers/DynamicEntitiesController.cs`**

```csharp
[HttpPost("{entityType}")]
public async Task<ActionResult<DynamicEntity>> Create(string entityType, [FromBody] DynamicEntity entity)
{
    try
    {
        if (!await _schemaRepository.SchemaExistsAsync(entityType))
        {
            return NotFound($"Entity type '{entityType}' not found. Create a schema first.");
        }

        // Get schema and ensure unique indexes
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        if (schema != null)
        {
            await _entityService.EnsureUniqueIndexesAsync(entityType, schema);
        }

        var created = await _entityService.CreateAsync(entityType, entity);
        return CreatedAtAction(nameof(GetById), 
            new { entityType = entityType, id = created.Id }, created);
    }
    catch (DuplicateEntityException ex)
    {
        return Conflict(new 
        { 
            message = ex.Message,
            entityType = ex.EntityType,
            field = ex.FieldName,
            value = ex.FieldValue,
            error = "DUPLICATE_ENTITY"
        });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating entity of type: {EntityType}", entityType);
        return StatusCode(500, "Internal server error");
    }
}
```

---

## Step 5: Update Service Interface

**File: `TestService.Api/Services/DynamicEntityService.cs`**

```csharp
public interface IDynamicEntityService
{
    // ...existing methods...
    Task EnsureUniqueIndexesAsync(string entityType, EntitySchema schema);
}

public class DynamicEntityService : IDynamicEntityService
{
    // ...existing code...

    public async Task EnsureUniqueIndexesAsync(string entityType, EntitySchema schema)
    {
        await _repository.EnsureUniqueIndexesAsync(entityType, schema);
    }
}
```

---

## Step 6: Update Frontend Error Handling

**File: `testservice-web/src/components/EntityCreateDialog.tsx`**

```typescript
const handleSubmit = async (e: React.FormEvent) => {
  e.preventDefault();
  setSubmitError('');

  if (!validateForm()) {
    return;
  }

  setIsSubmitting(true);

  try {
    const { apiService } = await import('../services/api');
    
    const entityData: any = {
      fields: formData
    };

    if (environment) {
      entityData.environment = environment;
    }

    await apiService.createEntity(entityType, entityData);
    
    // Reset form
    setFormData({});
    setEnvironment('');
    setErrors({});
    
    // Notify success
    onSuccess();
    onClose();
  } catch (err: any) {
    console.error('Failed to create entity:', err);
    
    // Handle duplicate entity error (409 Conflict)
    if (err.response?.status === 409) {
      const errorData = err.response?.data;
      
      if (errorData?.error === 'DUPLICATE_ENTITY') {
        // Show field-specific error
        const field = errorData.field || 'unknown field';
        const value = errorData.value || 'unknown value';
        
        setSubmitError(`Duplicate ${field}: An entity with ${field}="${value}" already exists`);
        
        // Highlight the duplicate field
        if (errorData.field && formData[errorData.field]) {
          setErrors(prev => ({
            ...prev,
            [errorData.field]: 'This value already exists'
          }));
        }
      } else {
        setSubmitError('This entity already exists');
      }
    } else {
      setSubmitError(err.response?.data?.message || 'Failed to create entity');
    }
  } finally {
    setIsSubmitting(false);
  }
};
```

---

## Step 7: Schema Builder Helper

**File: `TestService.Tests/Infrastructure/TestDataBuilders.cs`**

```csharp
public class EntitySchemaBuilder
{
    // ...existing code...

    private List<string> _uniqueFields = new();
    private bool _useCompoundUnique = false;

    public EntitySchemaBuilder WithUniqueField(string fieldName)
    {
        _uniqueFields.Add(fieldName);
        return this;
    }

    public EntitySchemaBuilder WithUniqueFields(params string[] fieldNames)
    {
        _uniqueFields.AddRange(fieldNames);
        return this;
    }

    public EntitySchemaBuilder WithCompoundUnique(bool useCompound = true)
    {
        _useCompoundUnique = useCompound;
        return this;
    }

    public EntitySchema Build()
    {
        return new EntitySchema
        {
            EntityName = _entityName,
            Fields = _fields,
            FilterableFields = _filterableFields,
            UniqueFields = _uniqueFields,
            UseCompoundUnique = _useCompoundUnique,
            ExcludeOnFetch = _excludeOnFetch
        };
    }
}
```

---

## Usage Examples

### Example 1: Username Must Be Unique

```csharp
// Create schema with unique username
var schema = new EntitySchemaBuilder()
    .WithEntityName("Agent")
    .WithField("username", "string", required: true)
    .WithField("email", "string", required: true)
    .WithField("brandId", "string")
    .WithUniqueField("username")  // ? Username must be unique
    .Build();

await schemaRepository.CreateSchemaAsync(schema);
```

**Result:**
- First entity with `username: "john.doe"` ? ? Created
- Second entity with `username: "john.doe"` ? ? 409 Conflict

### Example 2: Multiple Unique Fields

```csharp
// Both username and email must be unique
var schema = new EntitySchemaBuilder()
    .WithEntityName("User")
    .WithField("username", "string", required: true)
    .WithField("email", "string", required: true)
    .WithUniqueFields("username", "email")  // ? Both must be unique
    .Build();
```

**Result:**
- Separate unique indexes created for `username` and `email`
- Each field validated independently

### Example 3: Compound Unique Constraint

```csharp
// Combination of brandId + agentId must be unique
var schema = new EntitySchemaBuilder()
    .WithEntityName("BrandAgent")
    .WithField("brandId", "string", required: true)
    .WithField("agentId", "string", required: true)
    .WithUniqueFields("brandId", "agentId")
    .WithCompoundUnique(true)  // ? Compound unique index
    .Build();
```

**Result:**
- Single compound index: `{ brandId: 1, agentId: 1 }`
- Same `brandId` + `agentId` combination cannot exist twice
- Same `brandId` with different `agentId` is allowed

---

## API Examples

### Create Schema with Unique Field

```json
POST /api/schemas
{
  "entityName": "Agent",
  "fields": [
    { "name": "username", "type": "string", "required": true },
    { "name": "email", "type": "string", "required": true },
    { "name": "brandId", "type": "string" }
  ],
  "filterableFields": ["username", "brandId"],
  "uniqueFields": ["username"],
  "useCompoundUnique": false
}
```

### Create Duplicate Entity (Error Response)

```json
POST /api/entities/Agent
{
  "fields": {
    "username": "john.doe",  // Already exists
    "email": "john@example.com",
    "brandId": "brand123"
  }
}

// Response: 409 Conflict
{
  "message": "An entity with username='john.doe' already exists",
  "entityType": "Agent",
  "field": "username",
  "value": "john.doe",
  "error": "DUPLICATE_ENTITY"
}
```

---

## Test Cases

### Test Duplicate Detection

```csharp
[Test]
public async Task CreateEntity_WithDuplicateUniqueField_ReturnsConflict()
{
    // Arrange - Create schema with unique username
    var schema = new EntitySchemaBuilder()
        .WithEntityName("DuplicateTest")
        .WithField("username", "string", required: true)
        .WithUniqueField("username")
        .Build();
    await schemaRepository.CreateSchemaAsync(schema);

    // Create first entity
    var entity1 = new DynamicEntityBuilder()
        .WithField("username", "duplicate_user")
        .Build();
    await entityService.CreateAsync("DuplicateTest", entity1);

    // Act - Try to create duplicate
    var entity2 = new DynamicEntityBuilder()
        .WithField("username", "duplicate_user")  // Same username
        .Build();

    // Assert
    var ex = Assert.ThrowsAsync<DuplicateEntityException>(
        async () => await entityService.CreateAsync("DuplicateTest", entity2));
    
    Assert.That(ex.FieldName, Is.EqualTo("username"));
    Assert.That(ex.FieldValue, Is.EqualTo("duplicate_user"));
}
```

### Test Compound Unique

```csharp
[Test]
public async Task CreateEntity_WithDuplicateCompoundKey_ReturnsConflict()
{
    // Arrange - Create schema with compound unique
    var schema = new EntitySchemaBuilder()
        .WithEntityName("CompoundTest")
        .WithField("brandId", "string", required: true)
        .WithField("agentId", "string", required: true)
        .WithUniqueFields("brandId", "agentId")
        .WithCompoundUnique(true)
        .Build();
    await schemaRepository.CreateSchemaAsync(schema);

    // Create first entity
    var entity1 = new DynamicEntityBuilder()
        .WithField("brandId", "brand1")
        .WithField("agentId", "agent1")
        .Build();
    await entityService.CreateAsync("CompoundTest", entity1);

    // Act - Same combination should fail
    var entity2 = new DynamicEntityBuilder()
        .WithField("brandId", "brand1")
        .WithField("agentId", "agent1")  // Same combination
        .Build();

    // Assert
    Assert.ThrowsAsync<DuplicateEntityException>(
        async () => await entityService.CreateAsync("CompoundTest", entity2));

    // Different combination should succeed
    var entity3 = new DynamicEntityBuilder()
        .WithField("brandId", "brand1")
        .WithField("agentId", "agent2")  // Different agentId
        .Build();

    var created = await entityService.CreateAsync("CompoundTest", entity3);
    Assert.That(created.Id, Is.Not.Null);
}
```

---

## Frontend Display

### Error Toast Notification

```typescript
// In EntityCreateDialog or EntityList component

if (err.response?.status === 409 && err.response?.data?.error === 'DUPLICATE_ENTITY') {
  const { field, value } = err.response.data;
  
  // Show user-friendly toast
  toast.error(
    `Duplicate ${field}`,
    `An entity with ${field}="${value}" already exists. Please use a different value.`,
    {
      duration: 5000,
      icon: '??'
    }
  );
}
```

### Inline Field Error

```tsx
{/* In EntityCreateDialog */}
{errors[field.name] && (
  <p className="text-xs text-red-400 flex items-center gap-1 mt-1">
    <AlertCircle className="w-3 h-3" />
    {errors[field.name]}
  </p>
)}
```

---

## Migration Strategy

### For Existing Data

1. **Add unique fields to existing schemas:**
   ```bash
   PUT /api/schemas/Agent
   {
     ...existing schema...,
     "uniqueFields": ["username"],
     "useCompoundUnique": false
   }
   ```

2. **Clean up duplicates first:**
   ```javascript
   // MongoDB shell
   db.Dynamic_Agent.aggregate([
     { $group: { _id: "$username", count: { $sum: 1 }, ids: { $push: "$_id" } } },
     { $match: { count: { $gt: 1 } } }
   ])
   
   // Manually resolve duplicates
   ```

3. **Create indexes:**
   - Indexes are created automatically on first entity creation
   - Or manually trigger via API

---

## Summary

**What This Adds:**
- ? Schema-level unique field definitions
- ? Automatic MongoDB unique index creation
- ? Duplicate key exception handling
- ? User-friendly error messages
- ? Field-level error highlighting in UI
- ? Support for single and compound unique constraints

**Files to Create/Modify:**
1. **NEW**: `TestService.Api/Exceptions/DuplicateEntityException.cs`
2. **MODIFY**: `TestService.Api/Models/EntitySchema.cs` - Add `UniqueFields`, `UseCompoundUnique`
3. **MODIFY**: `TestService.Api/Services/DynamicEntityRepository.cs` - Add index management and error handling
4. **MODIFY**: `TestService.Api/Services/DynamicEntityService.cs` - Add `EnsureUniqueIndexesAsync`
5. **MODIFY**: `TestService.Api/Controllers/DynamicEntitiesController.cs` - Handle `DuplicateEntityException`
6. **MODIFY**: `testservice-web/src/components/EntityCreateDialog.tsx` - Display duplicate errors
7. **MODIFY**: `TestService.Tests/Infrastructure/TestDataBuilders.cs` - Add unique field support

**Ready to implement!** ??
