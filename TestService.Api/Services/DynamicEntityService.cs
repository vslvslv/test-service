using TestService.Api.Models;
using System.Globalization;
using System.Text.Json;

namespace TestService.Api.Services;

public interface IDynamicEntityService
{
    Task<IEnumerable<DynamicEntity>> GetAllAsync(string entityType, string? environment = null);
    Task<DynamicEntity?> GetByIdAsync(string entityType, string id);
    Task<DynamicEntity?> GetNextAvailableAsync(string entityType, string? environment = null);
    Task<IEnumerable<DynamicEntity>> GetByFieldValueAsync(string entityType, string fieldName, object value, string? environment = null);
    Task<DynamicEntity> CreateAsync(string entityType, DynamicEntity entity);
    Task<bool> UpdateAsync(string entityType, string id, DynamicEntity entity);
    Task<bool> DeleteAsync(string entityType, string id);
    Task<int> DeleteAllAsync(string entityType, string? environment = null);
    Task<bool> ResetConsumedAsync(string entityType, string id);
    Task<int> ResetAllConsumedAsync(string entityType, string? environment = null);
    Task<bool> ValidateEntityAsync(string entityType, DynamicEntity entity);
}

public class DynamicEntityService : IDynamicEntityService
{
    private readonly IDynamicEntityRepository _repository;
    private readonly IEntitySchemaRepository _schemaRepository;
    private readonly IMessageBusService _messageBus;
    private readonly IEnvironmentService _environmentService;
    private readonly ILogger<DynamicEntityService> _logger;

    public DynamicEntityService(
        IDynamicEntityRepository repository,
        IEntitySchemaRepository schemaRepository,
        IMessageBusService messageBus,
        IEnvironmentService environmentService,
        ILogger<DynamicEntityService> logger)
    {
        _repository = repository;
        _schemaRepository = schemaRepository;
        _messageBus = messageBus;
        _environmentService = environmentService;
        _logger = logger;
    }

    public async Task<IEnumerable<DynamicEntity>> GetAllAsync(string entityType, string? environment = null)
    {
        _logger.LogInformation("Retrieving all entities of type: {EntityType}, environment: {Environment}", 
            entityType, environment ?? "all");
        
        // Always return all entities (including consumed ones) so the UI can display them
        // The excludeOnFetch flag only affects GetNext, not GetAll
        return await _repository.GetAllAsync(entityType, excludeConsumed: false, environment);
    }

    public async Task<DynamicEntity?> GetByIdAsync(string entityType, string id)
    {
        _logger.LogInformation("Retrieving entity {EntityType} with ID: {Id}", entityType, id);
        
        // GetById should not auto-consume entities - that only happens via GetNext
        return await _repository.GetByIdAsync(entityType, id, markAsConsumed: false);
    }

    public async Task<DynamicEntity?> GetNextAvailableAsync(string entityType, string? environment = null)
    {
        _logger.LogInformation("Getting next available entity of type: {EntityType}, environment: {Environment}", 
            entityType, environment ?? "all");
        
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        if (schema == null)
        {
            _logger.LogWarning("Schema not found for entity type: {EntityType}", entityType);
            return null;
        }
        
        if (!schema.ExcludeOnFetch)
        {
            _logger.LogWarning("ExcludeOnFetch is not enabled for entity type: {EntityType}", entityType);
            return null;
        }
        
        var entity = await _repository.GetNextAvailableAsync(entityType, environment);
        
        if (entity != null)
        {
            await _messageBus.PublishAsync(new { 
                EntityType = entityType, 
                Id = entity.Id,
                Environment = environment,
                Action = "Consumed" 
            }, $"{entityType.ToLower()}.consumed");
            
            _logger.LogInformation("Entity {EntityType}/{Id} marked as consumed", entityType, entity.Id);
        }
        
        return entity;
    }

    public async Task<IEnumerable<DynamicEntity>> GetByFieldValueAsync(string entityType, string fieldName, object value, string? environment = null)
    {
        _logger.LogInformation("Retrieving entities {EntityType} where {Field}={Value}, environment: {Environment}", 
            entityType, fieldName, value, environment ?? "all");
        
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        bool excludeConsumed = schema?.ExcludeOnFetch ?? false;
        var normalizedValue = NormalizeFilterValue(schema, fieldName, value);
        
        return await _repository.GetByFieldValueAsync(entityType, fieldName, normalizedValue, excludeConsumed, environment);
    }

    public async Task<DynamicEntity> CreateAsync(string entityType, DynamicEntity entity)
    {
        _logger.LogInformation("Creating new entity of type: {EntityType}", entityType);
        
        // Validate environment exists if provided
        if (!string.IsNullOrEmpty(entity.Environment))
        {
            var environment = await _environmentService.GetByNameAsync(entity.Environment);
            if (environment == null)
            {
                _logger.LogWarning("Environment '{Environment}' not found", entity.Environment);
                throw new ArgumentException($"Environment '{entity.Environment}' does not exist. Please create it first.");
            }
        }
        
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        if (schema == null)
        {
            throw new ArgumentException($"Schema not found for entity type: {entityType}");
        }

        NormalizeEntityFields(entity, schema);

        // Validate against schema
        var validationResult = await ValidateEntityAsync(entityType, entity, schema);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(validationResult.Message);
        }

        entity.EntityType = entityType;
        var created = await _repository.CreateAsync(entity);
        
        await _messageBus.PublishAsync(created, $"{entityType.ToLower()}.created");
        _logger.LogInformation("Published message for created {EntityType}: {Id}", entityType, created.Id);
        
        return created;
    }

    public async Task<bool> UpdateAsync(string entityType, string id, DynamicEntity entity)
    {
        _logger.LogInformation("Updating entity {EntityType} with ID: {Id}", entityType, id);

        var existingEntity = await _repository.GetByIdAsync(entityType, id, markAsConsumed: false);
        if (existingEntity == null)
        {
            return false;
        }
        
        // Validate environment exists if provided
        if (!string.IsNullOrEmpty(entity.Environment))
        {
            var environment = await _environmentService.GetByNameAsync(entity.Environment);
            if (environment == null)
            {
                _logger.LogWarning("Environment '{Environment}' not found", entity.Environment);
                throw new ArgumentException($"Environment '{entity.Environment}' does not exist. Please create it first.");
            }
        }
        
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        if (schema == null)
        {
            throw new ArgumentException($"Schema not found for entity type: {entityType}");
        }

        entity.Id = id;
        NormalizeEntityFields(entity, schema);

        // Validate against schema
        var validationResult = await ValidateEntityAsync(entityType, entity, schema);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(validationResult.Message);
        }

        // Preserve immutable runtime metadata.
        entity.IsConsumed = existingEntity.IsConsumed;
        entity.CreatedAt = existingEntity.CreatedAt;
        entity.EntityType = entityType;
        var result = await _repository.UpdateAsync(entityType, id, entity);
        
        if (result)
        {
            entity.Id = id;
            await _messageBus.PublishAsync(entity, $"{entityType.ToLower()}.updated");
            _logger.LogInformation("Published message for updated {EntityType}: {Id}", entityType, id);
        }
        
        return result;
    }

    public async Task<bool> DeleteAsync(string entityType, string id)
    {
        _logger.LogInformation("Deleting entity {EntityType} with ID: {Id}", entityType, id);
        var result = await _repository.DeleteAsync(entityType, id);
        
        if (result)
        {
            await _messageBus.PublishAsync(new { EntityType = entityType, Id = id }, 
                $"{entityType.ToLower()}.deleted");
            _logger.LogInformation("Published message for deleted {EntityType}: {Id}", entityType, id);
        }
        
        return result;
    }

    public async Task<int> DeleteAllAsync(string entityType, string? environment = null)
    {
        _logger.LogInformation("Deleting all entities of type: {EntityType}, environment: {Environment}",
            entityType, environment ?? "all");

        var count = await _repository.DeleteAllAsync(entityType, environment);

        _logger.LogInformation("Deleted {Count} entities of type {EntityType}", count, entityType);
        return count;
    }

    public async Task<bool> ResetConsumedAsync(string entityType, string id)
    {
        _logger.LogInformation("Resetting consumed flag for {EntityType}/{Id}", entityType, id);
        return await _repository.ResetConsumedAsync(entityType, id);
    }

    public async Task<int> ResetAllConsumedAsync(string entityType, string? environment = null)
    {
        _logger.LogInformation("Resetting all consumed entities of type: {EntityType}, environment: {Environment}", 
            entityType, environment ?? "all");
        var count = await _repository.ResetAllConsumedAsync(entityType, environment);
        _logger.LogInformation("Reset {Count} consumed entities of type {EntityType}", count, entityType);
        return count;
    }

    public async Task<bool> ValidateEntityAsync(string entityType, DynamicEntity entity)
    {
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        var validationResult = await ValidateEntityAsync(entityType, entity, schema);
        return validationResult.IsValid;
    }

    private async Task<EntityValidationResult> ValidateEntityAsync(string entityType, DynamicEntity entity, EntitySchema? schema)
    {
        if (schema == null)
        {
            _logger.LogWarning("Schema not found for entity type: {EntityType}", entityType);
            return EntityValidationResult.Fail($"Schema not found for entity type: {entityType}");
        }

        // Validate required fields
        foreach (var field in schema.Fields.Where(f => f.Required))
        {
            if (!entity.Fields.ContainsKey(field.Name) || entity.Fields[field.Name] == null)
            {
                _logger.LogWarning("Required field {Field} missing for {EntityType}", field.Name, entityType);
                return EntityValidationResult.Fail($"Required field '{field.Name}' is missing.");
            }
        }

        // Collect unique fields from property-level configurations (isUnique = true)
        var individualUniqueFields = schema.Fields
            .Where(f => f.IsUnique)
            .Select(f => f.Name)
            .ToList();

        _logger.LogInformation("Validating entity {EntityType}. Found {Count} individual unique fields: {Fields}", 
            entityType, individualUniqueFields.Count, string.Join(", ", individualUniqueFields));

        // Get compound unique fields from schema-level configuration (uniqueFields array)
        var compoundUniqueFields = schema.UniqueFields ?? new List<string>();

        var existingEntities = await _repository.GetAllAsync(entityType, excludeConsumed: false, entity.Environment);

        // Check individual unique fields - each field must be unique independently
        foreach (var uniqueField in individualUniqueFields)
        {
            if (entity.Fields.ContainsKey(uniqueField))
            {
                var newValue = entity.Fields[uniqueField]?.ToString();
                
                foreach (var existingEntity in existingEntities)
                {
                    // Skip checking against the entity being updated (if it has an ID)
                    if (!string.IsNullOrEmpty(entity.Id) && existingEntity.Id == entity.Id)
                    {
                        continue;
                    }

                    if (existingEntity.Fields.ContainsKey(uniqueField))
                    {
                        var existingValue = existingEntity.Fields[uniqueField]?.ToString();
                        
                        if (newValue == existingValue)
                        {
                            _logger.LogWarning("Duplicate value found for unique field '{Field}' in {EntityType} (environment: '{Environment}'). Value: {Value}",
                                uniqueField, entityType, entity.Environment ?? "default", newValue);
                            return EntityValidationResult.Fail($"Field '{uniqueField}' must be unique. Value '{newValue}' already exists.");
                        }
                    }
                }
            }
        }

        // Check compound unique fields - all fields in the list must match together
        if (compoundUniqueFields.Any() && schema.UseCompoundUnique)
        {
            foreach (var existingEntity in existingEntities)
            {
                // Skip checking against the entity being updated (if it has an ID)
                if (!string.IsNullOrEmpty(entity.Id) && existingEntity.Id == entity.Id)
                {
                    continue;
                }

                // Check if all compound unique fields match
                bool allFieldsMatch = true;
                foreach (var uniqueField in compoundUniqueFields)
                {
                    if (!entity.Fields.ContainsKey(uniqueField) || !existingEntity.Fields.ContainsKey(uniqueField))
                    {
                        allFieldsMatch = false;
                        break;
                    }

                    var newValue = entity.Fields[uniqueField]?.ToString();
                    var existingValue = existingEntity.Fields[uniqueField]?.ToString();

                    if (newValue != existingValue)
                    {
                        allFieldsMatch = false;
                        break;
                    }
                }

                if (allFieldsMatch)
                {
                    var fieldNames = string.Join(", ", compoundUniqueFields);
                    _logger.LogWarning("Duplicate entity found for {EntityType} in environment '{Environment}'. Compound unique fields ({Fields}) must be unique together.",
                        entityType, entity.Environment ?? "default", fieldNames);
                    return EntityValidationResult.Fail($"Fields [{fieldNames}] must be unique together.");
                }
            }
        }

        return EntityValidationResult.Success();
    }

    private static void NormalizeEntityFields(DynamicEntity entity, EntitySchema schema)
    {
        foreach (var field in schema.Fields)
        {
            if (!entity.Fields.TryGetValue(field.Name, out var rawValue))
            {
                continue;
            }

            entity.Fields[field.Name] = NormalizeFieldValue(field, rawValue);
        }
    }

    private static object NormalizeFilterValue(EntitySchema? schema, string fieldName, object value)
    {
        if (schema == null)
        {
            return value;
        }

        var field = schema.Fields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.Ordinal));
        if (field == null)
        {
            return value;
        }

        return NormalizeFieldValue(field, value) ?? value;
    }

    private sealed class EntityValidationResult
    {
        public bool IsValid { get; init; }
        public string? Message { get; init; }

        public static EntityValidationResult Success() => new() { IsValid = true };
        public static EntityValidationResult Fail(string message) => new() { IsValid = false, Message = message };
    }

    private static object? NormalizeFieldValue(FieldDefinition field, object? rawValue)
    {
        if (rawValue == null)
        {
            return null;
        }

        return field.Type.ToLowerInvariant() switch
        {
            "string" => NormalizeStringValue(rawValue),
            "number" => NormalizeNumberValue(field.Name, rawValue),
            "boolean" => NormalizeBooleanValue(field.Name, rawValue),
            "date" => NormalizeDateTimeValue(field.Name, rawValue),
            "datetime" => NormalizeDateTimeValue(field.Name, rawValue),
            "array" => NormalizeStructuredValue(field.Name, rawValue, JsonValueKind.Array),
            "object" => NormalizeStructuredValue(field.Name, rawValue, JsonValueKind.Object),
            _ => rawValue
        };
    }

    private static string? NormalizeStringValue(object rawValue)
    {
        if (rawValue is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => element.GetString(),
                _ => element.ToString()
            };
        }

        return rawValue.ToString();
    }

    private static object? NormalizeNumberValue(string fieldName, object rawValue)
    {
        if (rawValue is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.TryGetInt64(out var longValue)
                    ? longValue
                    : element.GetDecimal();
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                return ParseNumber(fieldName, element.GetString());
            }
        }

        if (rawValue is string stringValue)
        {
            return ParseNumber(fieldName, stringValue);
        }

        if (rawValue is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return rawValue;
        }

        throw new ArgumentException($"Field '{fieldName}' must be a number.");
    }

    private static object? ParseNumber(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        throw new ArgumentException($"Field '{fieldName}' must be a valid number.");
    }

    private static object? NormalizeBooleanValue(string fieldName, object rawValue)
    {
        if (rawValue is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return element.GetBoolean();
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                if (string.IsNullOrWhiteSpace(element.GetString()))
                {
                    return null;
                }

                return ParseBoolean(fieldName, element.GetString());
            }
        }

        if (rawValue is bool boolValue)
        {
            return boolValue;
        }

        if (rawValue is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return null;
            }

            return ParseBoolean(fieldName, stringValue);
        }

        throw new ArgumentException($"Field '{fieldName}' must be a boolean.");
    }

    private static bool ParseBoolean(string fieldName, string? value)
    {
        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        throw new ArgumentException($"Field '{fieldName}' must be 'true' or 'false'.");
    }

    private static object? NormalizeDateTimeValue(string fieldName, object rawValue)
    {
        if (rawValue is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                if (string.IsNullOrWhiteSpace(element.GetString()))
                {
                    return null;
                }

                return ParseDateTime(fieldName, element.GetString());
            }
        }

        if (rawValue is DateTime dateTime)
        {
            return dateTime;
        }

        if (rawValue is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return null;
            }

            return ParseDateTime(fieldName, stringValue);
        }

        throw new ArgumentException($"Field '{fieldName}' must be a valid date.");
    }

    private static DateTime ParseDateTime(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Field '{fieldName}' must be a valid date.");
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Field '{fieldName}' must be a valid date.");
    }

    private static object? NormalizeStructuredValue(string fieldName, object rawValue, JsonValueKind expectedKind)
    {
        if (rawValue is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (element.ValueKind != expectedKind)
            {
                throw new ArgumentException($"Field '{fieldName}' must be a valid {expectedKind.ToString().ToLowerInvariant()}.");
            }

            return rawValue;
        }

        if (rawValue is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return null;
            }

            using var document = JsonDocument.Parse(stringValue);
            if (document.RootElement.ValueKind != expectedKind)
            {
                throw new ArgumentException($"Field '{fieldName}' must be a valid {expectedKind.ToString().ToLowerInvariant()}.");
            }

            return JsonSerializer.Deserialize<object>(document.RootElement.GetRawText());
        }

        return rawValue;
    }
}
