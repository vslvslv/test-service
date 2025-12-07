using Microsoft.AspNetCore.SignalR;
using TestService.Api.Hubs;

namespace TestService.Api.Services;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifySchemaCreated(string schemaName, object schema)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("SchemaCreated", new
            {
                type = "schema_created",
                schemaName,
                schema,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notification sent: Schema created - {SchemaName}", schemaName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send schema created notification for {SchemaName}", schemaName);
        }
    }

    public async Task NotifySchemaUpdated(string schemaName, object schema)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("SchemaUpdated", new
            {
                type = "schema_updated",
                schemaName,
                schema,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notification sent: Schema updated - {SchemaName}", schemaName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send schema updated notification for {SchemaName}", schemaName);
        }
    }

    public async Task NotifySchemaDeleted(string schemaName)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("SchemaDeleted", new
            {
                type = "schema_deleted",
                schemaName,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notification sent: Schema deleted - {SchemaName}", schemaName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send schema deleted notification for {SchemaName}", schemaName);
        }
    }

    public async Task NotifyEntityCreated(string entityType, string entityId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("EntityCreated", new
            {
                type = "entity_created",
                entityType,
                entityId,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notification sent: Entity created - {EntityType}/{EntityId}", entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send entity created notification for {EntityType}/{EntityId}", entityType, entityId);
        }
    }

    public async Task NotifyEntityUpdated(string entityType, string entityId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("EntityUpdated", new
            {
                type = "entity_updated",
                entityType,
                entityId,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notification sent: Entity updated - {EntityType}/{EntityId}", entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send entity updated notification for {EntityType}/{EntityId}", entityType, entityId);
        }
    }

    public async Task NotifyEntityDeleted(string entityType, string entityId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("EntityDeleted", new
            {
                type = "entity_deleted",
                entityType,
                entityId,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Notification sent: Entity deleted - {EntityType}/{EntityId}", entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send entity deleted notification for {EntityType}/{EntityId}", entityType, entityId);
        }
    }
}
