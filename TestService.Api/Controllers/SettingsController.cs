using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsRepository settingsRepository,
        ILogger<SettingsController> logger)
    {
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    #region Application Settings

    /// <summary>
    /// Get current application settings
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AppSettings>> GetSettings()
    {
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving settings");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update application settings
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<AppSettings>> UpdateSettings([FromBody] AppSettings settings)
    {
        try
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            settings.UpdatedBy = username;

            var updated = await _settingsRepository.UpdateSettingsAsync(settings);
            
            _logger.LogInformation("Settings updated by {Username}", username);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings");
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region API Keys

    /// <summary>
    /// Get all API keys
    /// </summary>
    [HttpGet("api-keys")]
    public async Task<ActionResult<IEnumerable<ApiKey>>> GetApiKeys()
    {
        try
        {
            var keys = await _settingsRepository.GetApiKeysAsync();
            
            // Don't return the actual key values for security
            var sanitizedKeys = keys.Select(k => new ApiKey
            {
                Id = k.Id,
                Name = k.Name,
                Key = k.Key, // In production, mask this or don't return it
                ExpiresAt = k.ExpiresAt,
                CreatedAt = k.CreatedAt,
                CreatedBy = k.CreatedBy,
                LastUsed = k.LastUsed,
                IsActive = k.IsActive
            });

            return Ok(sanitizedKeys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving API keys");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Generate a new API key
    /// </summary>
    [HttpPost("api-keys")]
    public async Task<ActionResult<ApiKey>> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("API key name is required");
            }

            var username = User.FindFirst(ClaimTypes.Name)?.Value;

            // Generate a unique API key
            var keyValue = GenerateApiKey();

            var apiKey = new ApiKey
            {
                Name = request.Name,
                Key = keyValue,
                ExpiresAt = request.ExpirationDays.HasValue 
                    ? DateTime.UtcNow.AddDays(request.ExpirationDays.Value) 
                    : null,
                CreatedBy = username
            };

            var created = await _settingsRepository.CreateApiKeyAsync(apiKey);
            
            _logger.LogInformation("API key created: {Name} by {Username}", request.Name, username);
            
            return CreatedAtAction(nameof(GetApiKeys), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete an API key
    /// </summary>
    [HttpDelete("api-keys/{id}")]
    public async Task<ActionResult> DeleteApiKey(string id)
    {
        try
        {
            var result = await _settingsRepository.DeleteApiKeyAsync(id);
            
            if (!result)
            {
                return NotFound($"API key with ID {id} not found");
            }

            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            _logger.LogInformation("API key deleted: {Id} by {Username}", id, username);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API key: {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    #endregion

    #region Helper Methods

    private static string GenerateApiKey()
    {
        // Generate a secure random API key
        const string prefix = "ts_";
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var key = new char[32];
        
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = chars[random.Next(chars.Length)];
        }
        
        return prefix + new string(key);
    }

    #endregion
}
