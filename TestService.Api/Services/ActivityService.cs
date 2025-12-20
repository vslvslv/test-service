using Microsoft.AspNetCore.SignalR;
using TestService.Api.Hubs;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IActivityService
{
    Task LogActivityAsync(Activity activity);
    Task<Activity> LogActivityAsync(
        string type,
        string action,
        string user,
        string description,
        string? entityType = null,
        string? entityId = null,
        string? environment = null,
        ActivityDetails? details = null);
    Task<List<Activity>> GetActivitiesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? entityType = null,
        string? type = null,
        string? action = null,
        string? user = null,
        int skip = 0,
        int limit = 100);
    Task<int> GetCountAsync(DateTime? startDate = null, DateTime? endDate = null);
}

public class ActivityService : IActivityService
{
    private readonly IActivityRepository _activityRepository;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(
        IActivityRepository activityRepository,
        IHubContext<NotificationHub> hubContext,
        ILogger<ActivityService> logger)
    {
        _activityRepository = activityRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task LogActivityAsync(Activity activity)
    {
        try
        {
            // Save to database
            var savedActivity = await _activityRepository.CreateAsync(activity);

            // Send real-time notification via SignalR
            await _hubContext.Clients.All.SendAsync("ActivityCreated", savedActivity);

            _logger.LogInformation(
                "Activity logged: {Type}/{Action} by {User} - {Description}",
                activity.Type, activity.Action, activity.User, activity.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log activity: {Description}", activity.Description);
            // Don't throw - we don't want to fail the main operation if activity logging fails
        }
    }

    public async Task<Activity> LogActivityAsync(
        string type,
        string action,
        string user,
        string description,
        string? entityType = null,
        string? entityId = null,
        string? environment = null,
        ActivityDetails? details = null)
    {
        var activity = new Activity
        {
            Type = type,
            Action = action,
            User = user,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            Environment = environment,
            Details = details
        };

        await LogActivityAsync(activity);
        return activity;
    }

    public async Task<List<Activity>> GetActivitiesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? entityType = null,
        string? type = null,
        string? action = null,
        string? user = null,
        int skip = 0,
        int limit = 100)
    {
        return await _activityRepository.GetActivitiesAsync(
            startDate, endDate, entityType, type, action, user, skip, limit);
    }

    public async Task<int> GetCountAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        return await _activityRepository.GetCountAsync(startDate, endDate);
    }
}
