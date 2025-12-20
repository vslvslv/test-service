using TestService.Api.Services;

namespace TestService.Api.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up old activities (older than 7 days)
/// </summary>
public class ActivityCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActivityCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24); // Run daily

    public ActivityCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ActivityCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Activity Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldActivitiesAsync();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Activity Cleanup Service");
                // Wait before retrying
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Activity Cleanup Service stopped");
    }

    private async Task CleanupOldActivitiesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var activityRepository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        _logger.LogInformation("Starting activity cleanup...");

        var deletedCount = await activityRepository.DeleteOldActivitiesAsync(daysToKeep: 7);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} old activities", deletedCount);
        }
        else
        {
            _logger.LogDebug("No old activities to delete");
        }
    }
}
