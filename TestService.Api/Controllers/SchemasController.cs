using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchemasController : ControllerBase
{
    private readonly IEntitySchemaRepository _schemaRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SchemasController> _logger;

    public SchemasController(
        IEntitySchemaRepository schemaRepository,
        INotificationService notificationService,
        ILogger<SchemasController> logger)
    {
        _schemaRepository = schemaRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all registered entity schemas
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EntitySchema>>> GetAll()
    {
        try
        {
            var schemas = await _schemaRepository.GetAllSchemasAsync();
            return Ok(schemas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schemas");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get schema by entity name
    /// </summary>
    [HttpGet("{entityName}")]
    public async Task<ActionResult<EntitySchema>> GetByName(string entityName)
    {
        try
        {
            var schema = await _schemaRepository.GetSchemaByNameAsync(entityName);
            if (schema == null)
            {
                return NotFound($"Schema for entity '{entityName}' not found");
            }
            return Ok(schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema for {EntityName}", entityName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new entity schema
    /// </summary>
    /// <remarks>
    /// Example request:
    /// 
    ///     POST /api/schemas
    ///     {
    ///       "entityName": "Agent",
    ///       "fields": [
    ///         { "name": "username", "type": "string", "required": true },
    ///         { "name": "password", "type": "string", "required": true },
    ///         { "name": "userId", "type": "string", "required": true },
    ///         { "name": "firstName", "type": "string", "required": false },
    ///         { "name": "lastName", "type": "string", "required": false },
    ///         { "name": "brandId", "type": "string", "required": false },
    ///         { "name": "labelId", "type": "string", "required": false },
    ///         { "name": "orientationType", "type": "string", "required": false },
    ///         { "name": "agentType", "type": "string", "required": false }
    ///       ],
    ///       "filterableFields": ["username", "brandId", "labelId", "orientationType", "agentType"],
    ///       "excludeOnFetch": true
    ///     }
    ///     
    /// Set excludeOnFetch=true for test objects that should be automatically marked as consumed
    /// when fetched, preventing them from being used in multiple parallel tests.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<EntitySchema>> Create([FromBody] EntitySchema schema)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(schema.EntityName))
            {
                return BadRequest("Entity name is required");
            }

            if (await _schemaRepository.SchemaExistsAsync(schema.EntityName))
            {
                return Conflict($"Schema for entity '{schema.EntityName}' already exists");
            }

            var created = await _schemaRepository.CreateSchemaAsync(schema);
            
            // Send notification
            await _notificationService.NotifySchemaCreated(created.EntityName, created);
            
            return CreatedAtAction(nameof(GetByName), new { entityName = created.EntityName }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schema");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing entity schema
    /// </summary>
    [HttpPut("{entityName}")]
    public async Task<ActionResult> Update(string entityName, [FromBody] EntitySchema schema)
    {
        try
        {
            schema.EntityName = entityName;
            var result = await _schemaRepository.UpdateSchemaAsync(entityName, schema);
            if (!result)
            {
                return NotFound($"Schema for entity '{entityName}' not found");
            }
            
            // Send notification
            await _notificationService.NotifySchemaUpdated(entityName, schema);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schema for {EntityName}", entityName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an entity schema
    /// </summary>
    [HttpDelete("{entityName}")]
    public async Task<ActionResult> Delete(string entityName)
    {
        try
        {
            var result = await _schemaRepository.DeleteSchemaAsync(entityName);
            if (!result)
            {
                return NotFound($"Schema for entity '{entityName}' not found");
            }
            
            // Send notification
            await _notificationService.NotifySchemaDeleted(entityName);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schema for {EntityName}", entityName);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete all entities for a specific schema
    /// </summary>
    /// <param name="entityName">The entity type/schema name</param>
    /// <param name="environment">Optional: Only delete entities in this environment</param>
    /// <remarks>
    /// This will delete all entities of the specified type, but the schema itself will remain intact.
    /// Use this for bulk cleanup operations.
    /// 
    /// Example: DELETE /api/schemas/Agent/entities?environment=dev
    /// </remarks>
    [HttpDelete("{entityName}/entities")]
    public async Task<ActionResult> DeleteAllEntities(
        string entityName,
        [FromQuery] string? environment = null)
    {
        try
        {
            // Check if schema exists
            var schema = await _schemaRepository.GetSchemaByNameAsync(entityName);
            if (schema == null)
            {
                return NotFound($"Schema for entity '{entityName}' not found");
            }

            // Delete all entities using the entity service (requires injection)
            var entityService = HttpContext.RequestServices.GetRequiredService<IDynamicEntityService>();
            var activityService = HttpContext.RequestServices.GetRequiredService<IActivityService>();
            
            var count = await entityService.DeleteAllAsync(entityName, environment);
            
            // Log activity - bulk delete
            var user = User?.Identity?.Name ?? "anonymous";
            await activityService.LogActivityAsync(
                ActivityType.Entity,
                ActivityAction.BulkDeleted,
                user,
                $"Bulk delete: {count} entities deleted from {entityName}",
                entityName,
                null,
                environment,
                new ActivityDetails { Count = count }
            );
            
            // Send notification
            await _notificationService.NotifyBulkAction(
                entityName, 
                "deleted", 
                count, 
                environment);
            
            return Ok(new { deletedCount = count, message = $"Deleted {count} entities from {entityName}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all entities for {EntityName}", entityName);
            return StatusCode(500, "Internal server error");
        }
    }
}
