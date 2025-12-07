using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/entities")]
public class DynamicEntitiesController : ControllerBase
{
    private readonly IDynamicEntityService _entityService;
    private readonly IEntitySchemaRepository _schemaRepository;
    private readonly ILogger<DynamicEntitiesController> _logger;

    public DynamicEntitiesController(
        IDynamicEntityService entityService,
        IEntitySchemaRepository schemaRepository,
        ILogger<DynamicEntitiesController> logger)
    {
        _entityService = entityService;
        _schemaRepository = schemaRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all entities of a specific type
    /// </summary>
    /// <param name="entityType">The entity type</param>
    /// <param name="environment">Optional: Filter by environment name</param>
    /// <remarks>
    /// If the schema has excludeOnFetch=true, this will only return non-consumed entities.
    /// 
    /// Example: GET /api/entities/Agent?environment=dev
    /// </remarks>
    [HttpGet("{entityType}")]
    public async Task<ActionResult<IEnumerable<DynamicEntity>>> GetAll(
        string entityType,
        [FromQuery] string? environment = null)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found. Create a schema first.");
            }

            var entities = await _entityService.GetAllAsync(entityType, environment);
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entities for type: {EntityType}", entityType);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get entity by ID
    /// </summary>
    /// <remarks>
    /// If the schema has excludeOnFetch=true, this will mark the entity as consumed
    /// </remarks>
    [HttpGet("{entityType}/{id}")]
    public async Task<ActionResult<DynamicEntity>> GetById(string entityType, string id)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            var entity = await _entityService.GetByIdAsync(entityType, id);
            if (entity == null)
            {
                return NotFound();
            }
            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving entity {EntityType}/{Id}", entityType, id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get next available (non-consumed) entity - atomically marks it as consumed
    /// </summary>
    /// <remarks>
    /// This endpoint is specifically designed for parallel test execution.
    /// It atomically finds and marks an entity as consumed in a single operation,
    /// preventing race conditions when multiple tests run in parallel.
    /// 
    /// Only works if the schema has excludeOnFetch=true.
    /// 
    /// Example: GET /api/entities/Agent/next
    /// </remarks>
    [HttpGet("{entityType}/next")]
    public async Task<ActionResult<DynamicEntity>> GetNextAvailable(string entityType)
    {
        try
        {
            var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
            if (schema == null)
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            if (!schema.ExcludeOnFetch)
            {
                return BadRequest($"Entity type '{entityType}' does not have excludeOnFetch enabled");
            }

            var entity = await _entityService.GetNextAvailableAsync(entityType);
            if (entity == null)
            {
                return NotFound("No available entities found");
            }

            return Ok(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next available entity for type: {EntityType}", entityType);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get entities filtered by a field value
    /// </summary>
    /// <remarks>
    /// Example: GET /api/entities/Agent/filter/brandId/brand123
    /// 
    /// If the schema has excludeOnFetch=true, this will only return non-consumed entities
    /// </remarks>
    [HttpGet("{entityType}/filter/{fieldName}/{value}")]
    public async Task<ActionResult<IEnumerable<DynamicEntity>>> GetByFieldValue(
        string entityType, 
        string fieldName, 
        string value)
    {
        try
        {
            var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
            if (schema == null)
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            if (!schema.FilterableFields.Contains(fieldName))
            {
                return BadRequest($"Field '{fieldName}' is not marked as filterable for entity type '{entityType}'");
            }

            var entities = await _entityService.GetByFieldValueAsync(entityType, fieldName, value);
            return Ok(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering entities {EntityType} by {Field}={Value}", 
                entityType, fieldName, value);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new entity
    /// </summary>
    /// <remarks>
    /// Example request:
    /// 
    ///     POST /api/entities/Agent
    ///     {
    ///       "fields": {
    ///         "username": "john.doe",
    ///         "password": "SecurePass@123",
    ///         "userId": "user001",
    ///         "firstName": "John",
    ///         "lastName": "Doe",
    ///         "brandId": "brand123",
    ///         "labelId": "label456",
    ///         "orientationType": "vertical",
    ///         "agentType": "support"
    ///       }
    ///     }
    /// </remarks>
    [HttpPost("{entityType}")]
    public async Task<ActionResult<DynamicEntity>> Create(string entityType, [FromBody] DynamicEntity entity)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found. Create a schema first.");
            }

            var created = await _entityService.CreateAsync(entityType, entity);
            return CreatedAtAction(nameof(GetById), 
                new { entityType = entityType, id = created.Id }, created);
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

    /// <summary>
    /// Update an existing entity
    /// </summary>
    [HttpPut("{entityType}/{id}")]
    public async Task<ActionResult> Update(string entityType, string id, [FromBody] DynamicEntity entity)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            var result = await _entityService.UpdateAsync(entityType, id, entity);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating entity {EntityType}/{Id}", entityType, id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an entity
    /// </summary>
    [HttpDelete("{entityType}/{id}")]
    public async Task<ActionResult> Delete(string entityType, string id)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            var result = await _entityService.DeleteAsync(entityType, id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting entity {EntityType}/{Id}", entityType, id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Reset the consumed flag for a specific entity
    /// </summary>
    /// <remarks>
    /// Makes the entity available again for fetching
    /// </remarks>
    [HttpPost("{entityType}/{id}/reset")]
    public async Task<ActionResult> ResetConsumed(string entityType, string id)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            var result = await _entityService.ResetConsumedAsync(entityType, id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting consumed flag for {EntityType}/{Id}", entityType, id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Reset all consumed entities of a specific type
    /// </summary>
    /// <remarks>
    /// Useful for cleaning up after test runs. Returns the count of reset entities.
    /// </remarks>
    [HttpPost("{entityType}/reset-all")]
    public async Task<ActionResult<int>> ResetAllConsumed(string entityType)
    {
        try
        {
            if (!await _schemaRepository.SchemaExistsAsync(entityType))
            {
                return NotFound($"Entity type '{entityType}' not found");
            }

            var count = await _entityService.ResetAllConsumedAsync(entityType);
            return Ok(new { resetCount = count, message = $"Reset {count} consumed entities" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting all consumed entities for type: {EntityType}", entityType);
            return StatusCode(500, "Internal server error");
        }
    }
}
