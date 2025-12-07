using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnvironmentsController : ControllerBase
{
    private readonly IEnvironmentService _environmentService;
    private readonly ILogger<EnvironmentsController> _logger;

    public EnvironmentsController(
        IEnvironmentService environmentService,
        ILogger<EnvironmentsController> logger)
    {
        _environmentService = environmentService;
        _logger = logger;
    }

    /// <summary>
    /// Get all environments
    /// </summary>
    /// <param name="includeInactive">Include inactive environments</param>
    /// <param name="includeStatistics">Include entity statistics for each environment</param>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EnvironmentResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool includeStatistics = false)
    {
        try
        {
            var environments = await _environmentService.GetAllAsync(includeInactive, includeStatistics);
            return Ok(environments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environments");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get environment by ID
    /// </summary>
    /// <param name="id">Environment ID</param>
    /// <param name="includeStatistics">Include entity statistics</param>
    [HttpGet("{id}")]
    public async Task<ActionResult<EnvironmentResponse>> GetById(
        string id,
        [FromQuery] bool includeStatistics = false)
    {
        try
        {
            var environment = await _environmentService.GetByIdAsync(id, includeStatistics);
            if (environment == null)
            {
                return NotFound();
            }
            return Ok(environment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environment {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get environment by name
    /// </summary>
    /// <param name="name">Environment name (e.g., "dev", "staging", "production")</param>
    /// <param name="includeStatistics">Include entity statistics</param>
    [HttpGet("name/{name}")]
    public async Task<ActionResult<EnvironmentResponse>> GetByName(
        string name,
        [FromQuery] bool includeStatistics = false)
    {
        try
        {
            var environment = await _environmentService.GetByNameAsync(name, includeStatistics);
            if (environment == null)
            {
                return NotFound();
            }
            return Ok(environment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environment {Name}", name);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get statistics for an environment
    /// </summary>
    /// <param name="name">Environment name</param>
    [HttpGet("{name}/statistics")]
    public async Task<ActionResult<EnvironmentStatistics>> GetStatistics(string name)
    {
        try
        {
            var stats = await _environmentService.GetStatisticsAsync(name);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics for environment {Name}", name);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new environment
    /// </summary>
    /// <remarks>
    /// Example request:
    /// 
    ///     POST /api/environments
    ///     {
    ///       "name": "qa",
    ///       "displayName": "QA Environment",
    ///       "description": "Quality Assurance testing environment",
    ///       "url": "https://qa.example.com",
    ///       "color": "#0000ff",
    ///       "order": 4,
    ///       "configuration": {
    ///         "apiKey": "qa-api-key",
    ///         "timeout": "30"
    ///       },
    ///       "tags": ["qa", "testing"]
    ///     }
    /// 
    /// Name must be lowercase alphanumeric with hyphens only.
    /// Admin role required.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EnvironmentResponse>> Create([FromBody] CreateEnvironmentRequest request)
    {
        try
        {
            var username = User.Identity?.Name;
            var environment = await _environmentService.CreateAsync(request, username);
            return CreatedAtAction(nameof(GetById), new { id = environment.Id }, environment);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating environment");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an environment
    /// </summary>
    /// <remarks>
    /// Example request:
    /// 
    ///     PUT /api/environments/{id}
    ///     {
    ///       "displayName": "QA Updated",
    ///       "description": "Updated description",
    ///       "url": "https://qa-new.example.com",
    ///       "isActive": true,
    ///       "order": 5
    ///     }
    /// 
    /// All fields are optional. Only provided fields will be updated.
    /// Admin role required.
    /// </remarks>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(string id, [FromBody] UpdateEnvironmentRequest request)
    {
        try
        {
            var result = await _environmentService.UpdateAsync(id, request);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating environment {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an environment
    /// </summary>
    /// <remarks>
    /// Cannot delete an environment that contains entities.
    /// Delete all entities in the environment first.
    /// Admin role required.
    /// </remarks>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var result = await _environmentService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting environment {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Activate an environment
    /// </summary>
    [HttpPost("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Activate(string id)
    {
        try
        {
            var request = new UpdateEnvironmentRequest { IsActive = true };
            var result = await _environmentService.UpdateAsync(id, request);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating environment {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Deactivate an environment
    /// </summary>
    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Deactivate(string id)
    {
        try
        {
            var request = new UpdateEnvironmentRequest { IsActive = false };
            var result = await _environmentService.UpdateAsync(id, request);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating environment {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
