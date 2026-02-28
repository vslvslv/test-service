using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MocksController : ControllerBase
{
    private readonly IMockService _mockService;
    private readonly ILogger<MocksController> _logger;

    public MocksController(IMockService mockService, ILogger<MocksController> logger)
    {
        _mockService = mockService;
        _logger = logger;
    }

    [HttpPost("expectations")]
    [Authorize(Policy = PermissionDefinitions.MocksWrite)]
    public async Task<ActionResult<MockExpectation>> CreateExpectation([FromBody] MockExpectation expectation)
    {
        try
        {
            var created = await _mockService.CreateExpectationAsync(expectation);
            return CreatedAtAction(nameof(GetExpectations), new { environment = created.Environment }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating mock expectation");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("expectations")]
    [Authorize(Policy = PermissionDefinitions.MocksRead)]
    public async Task<ActionResult<IEnumerable<MockExpectation>>> GetExpectations(
        [FromQuery] string? environment = null,
        [FromQuery] bool includeDisabled = false)
    {
        try
        {
            var expectations = await _mockService.GetExpectationsAsync(environment, includeDisabled);
            return Ok(expectations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mock expectations");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("expectations/{id}")]
    [Authorize(Policy = PermissionDefinitions.MocksWrite)]
    public async Task<ActionResult> UpdateExpectation(string id, [FromBody] MockExpectation expectation)
    {
        try
        {
            var result = await _mockService.UpdateExpectationAsync(id, expectation);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating mock expectation {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("expectations/{id}")]
    [Authorize(Policy = PermissionDefinitions.MocksWrite)]
    public async Task<ActionResult> DeleteExpectation(string id)
    {
        try
        {
            var result = await _mockService.DeleteExpectationAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting mock expectation {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("requests")]
    [Authorize(Policy = PermissionDefinitions.MocksLogsRead)]
    public async Task<ActionResult<IEnumerable<MockRequestLog>>> GetRequestLogs(
        [FromQuery] string? environment = null,
        [FromQuery] string? path = null,
        [FromQuery] int limit = 100,
        [FromQuery] bool? matched = null)
    {
        try
        {
            var logs = await _mockService.GetRequestLogsAsync(environment, path, limit, matched);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mock request logs");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("requests")]
    [Authorize(Policy = PermissionDefinitions.MocksLogsDelete)]
    public async Task<ActionResult<object>> DeleteRequestLogs([FromQuery] string? environment = null)
    {
        try
        {
            var deletedCount = await _mockService.DeleteRequestLogsAsync(environment);
            return Ok(new { deletedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting mock request logs");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("verify")]
    [Authorize(Policy = PermissionDefinitions.MocksVerify)]
    public async Task<ActionResult<MockVerificationResponse>> Verify([FromBody] MockVerificationRequest request)
    {
        try
        {
            var result = await _mockService.VerifyAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying mock requests");
            return StatusCode(500, "Internal server error");
        }
    }
}
