using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Agent>>> GetAll()
    {
        try
        {
            var agents = await _agentService.GetAllAsync();
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all agents");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Agent>> GetById(string id)
    {
        try
        {
            var agent = await _agentService.GetByIdAsync(id);
            if (agent == null)
            {
                return NotFound();
            }
            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("username/{username}")]
    public async Task<ActionResult<Agent>> GetByUsername(string username)
    {
        try
        {
            var agent = await _agentService.GetByUsernameAsync(username);
            if (agent == null)
            {
                return NotFound();
            }
            return Ok(agent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent with username: {Username}", username);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("brand/{brandId}")]
    public async Task<ActionResult<IEnumerable<Agent>>> GetByBrandId(string brandId)
    {
        try
        {
            var agents = await _agentService.GetByBrandIdAsync(brandId);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents for brand: {BrandId}", brandId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("label/{labelId}")]
    public async Task<ActionResult<IEnumerable<Agent>>> GetByLabelId(string labelId)
    {
        try
        {
            var agents = await _agentService.GetByLabelIdAsync(labelId);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents for label: {LabelId}", labelId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("orientation/{orientationType}")]
    public async Task<ActionResult<IEnumerable<Agent>>> GetByOrientationType(string orientationType)
    {
        try
        {
            var agents = await _agentService.GetByOrientationTypeAsync(orientationType);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents with orientation: {OrientationType}", orientationType);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("type/{agentType}")]
    public async Task<ActionResult<IEnumerable<Agent>>> GetByAgentType(string agentType)
    {
        try
        {
            var agents = await _agentService.GetByAgentTypeAsync(agentType);
            return Ok(agents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents with type: {AgentType}", agentType);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Agent>> Create([FromBody] Agent agent)
    {
        try
        {
            var created = await _agentService.CreateAsync(agent);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] Agent agent)
    {
        try
        {
            var result = await _agentService.UpdateAsync(id, agent);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var result = await _agentService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting agent with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
