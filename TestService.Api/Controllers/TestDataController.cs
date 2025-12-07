using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestDataController : ControllerBase
{
    private readonly ITestDataService _testDataService;
    private readonly ILogger<TestDataController> _logger;

    public TestDataController(ITestDataService testDataService, ILogger<TestDataController> logger)
    {
        _testDataService = testDataService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TestData>>> GetAll()
    {
        try
        {
            var data = await _testDataService.GetAllAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all test data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TestData>> GetById(string id)
    {
        try
        {
            var data = await _testDataService.GetByIdAsync(id);
            if (data == null)
            {
                return NotFound();
            }
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving test data with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<TestData>>> GetByCategory(string category)
    {
        try
        {
            var data = await _testDataService.GetByCategoryAsync(category);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving test data for category: {Category}", category);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("aggregated")]
    public async Task<ActionResult<Dictionary<string, decimal>>> GetAggregatedByCategory()
    {
        try
        {
            var data = await _testDataService.GetAggregatedDataByCategoryAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating test data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<TestData>> Create([FromBody] TestData testData)
    {
        try
        {
            var created = await _testDataService.CreateAsync(testData);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test data");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] TestData testData)
    {
        try
        {
            var result = await _testDataService.UpdateAsync(id, testData);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating test data with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var result = await _testDataService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting test data with ID: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
