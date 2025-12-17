using TestService.Api.Models;

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
        
        return await _repository.GetByFieldValueAsync(entityType, fieldName, value, excludeConsumed, environment);
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
        
        // Validate against schema
        if (!await ValidateEntityAsync(entityType, entity))
        {
            throw new ArgumentException($"Entity does not match schema for type: {entityType}");
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
        
        // Validate against schema
        if (!await ValidateEntityAsync(entityType, entity))
        {
            throw new ArgumentException($"Entity does not match schema for type: {entityType}");
        }

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
        if (schema == null)
        {
            _logger.LogWarning("Schema not found for entity type: {EntityType}", entityType);
            return false;
        }

        // Validate required fields
        foreach (var field in schema.Fields.Where(f => f.Required))
        {
            if (!entity.Fields.ContainsKey(field.Name) || entity.Fields[field.Name] == null)
            {
                _logger.LogWarning("Required field {Field} missing for {EntityType}", field.Name, entityType);
                return false;
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
                            return false;
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
                    return false;
                }
            }
        }

        return true;
    }
}
