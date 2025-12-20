using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/activities")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ILogger<ActivitiesController> _logger;

    public ActivitiesController(
        IActivityService activityService,
        ILogger<ActivitiesController> logger)
    {
        _activityService = activityService;
        _logger = logger;
    }

    /// <summary>
    /// Get activities with optional filters
    /// </summary>
    /// <param name="startDate">Start date for filtering (ISO 8601 format)</param>
    /// <param name="endDate">End date for filtering (ISO 8601 format)</param>
    /// <param name="entityType">Filter by entity type (e.g., test-agent, product)</param>
    /// <param name="type">Filter by activity type (entity, schema, user, environment)</param>
    /// <param name="action">Filter by action (created, updated, deleted, consumed, reset)</param>
    /// <param name="user">Filter by user</param>
    /// <param name="skip">Number of records to skip for pagination</param>
    /// <param name="limit">Maximum number of records to return (max 500)</param>
    /// <remarks>
    /// Example: GET /api/activities?entityType=test-agent&amp;startDate=2025-12-13T00:00:00Z&amp;limit=50
    /// 
    /// Defaults to last 7 days if no date range specified.
    /// Returns activities sorted by timestamp (newest first).
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<ActivityListResponse>> GetActivities(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? type = null,
        [FromQuery] string? action = null,
        [FromQuery] string? user = null,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 100)
    {
        try
        {
            // Enforce maximum limit
            limit = Math.Min(limit, 500);

            var activities = await _activityService.GetActivitiesAsync(
                startDate, endDate, entityType, type, action, user, skip, limit);

            var totalCount = await _activityService.GetCountAsync(startDate, endDate);

            return Ok(new ActivityListResponse
            {
                Activities = activities,
                TotalCount = totalCount,
                Skip = skip,
                Limit = limit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activities");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get recent activities (last 24 hours by default)
    /// </summary>
    /// <param name="hours">Number of hours to look back (default: 24, max: 168 = 7 days)</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <remarks>
    /// Example: GET /api/activities/recent?hours=48&amp;limit=50
    /// 
    /// Useful for real-time dashboard views.
    /// </remarks>
    [HttpGet("recent")]
    public async Task<ActionResult<List<Activity>>> GetRecentActivities(
        [FromQuery] int hours = 24,
        [FromQuery] int limit = 100)
    {
        try
        {
            // Enforce maximum hours (7 days)
            hours = Math.Min(hours, 168);
            limit = Math.Min(limit, 500);

            var startDate = DateTime.UtcNow.AddHours(-hours);
            var activities = await _activityService.GetActivitiesAsync(
                startDate: startDate,
                limit: limit);

            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activities");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get activity statistics
    /// </summary>
    /// <param name="startDate">Start date for statistics</param>
    /// <param name="endDate">End date for statistics</param>
    /// <remarks>
    /// Example: GET /api/activities/stats?startDate=2025-12-13T00:00:00Z
    /// 
    /// Returns counts by activity type and action.
    /// </remarks>
    [HttpGet("stats")]
    public async Task<ActionResult<ActivityStats>> GetActivityStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            // Default to last 7 days
            startDate ??= DateTime.UtcNow.AddDays(-7);
            endDate ??= DateTime.UtcNow;

            var allActivities = await _activityService.GetActivitiesAsync(
                startDate: startDate,
                endDate: endDate,
                limit: 10000); // Get all for stats

            var stats = new ActivityStats
            {
                TotalActivities = allActivities.Count,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                ByType = allActivities.GroupBy(a => a.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByAction = allActivities.GroupBy(a => a.Action)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByEntityType = allActivities
                    .Where(a => !string.IsNullOrEmpty(a.EntityType))
                    .GroupBy(a => a.EntityType!)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByUser = allActivities.GroupBy(a => a.User)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activity statistics");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class ActivityListResponse
{
    public List<Activity> Activities { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Limit { get; set; }
    public bool HasMore => Skip + Activities.Count < TotalCount;
}

public class ActivityStats
{
    public int TotalActivities { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
    public Dictionary<string, int> ByAction { get; set; } = new();
    public Dictionary<string, int> ByEntityType { get; set; } = new();
    public Dictionary<string, int> ByUser { get; set; } = new();
}
