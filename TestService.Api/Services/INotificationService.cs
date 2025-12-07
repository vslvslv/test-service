namespace TestService.Api.Services;

public interface INotificationService
{
    Task NotifySchemaCreated(string schemaName, object schema);
    Task NotifySchemaUpdated(string schemaName, object schema);
    Task NotifySchemaDeleted(string schemaName);
    Task NotifyEntityCreated(string entityType, string entityId);
    Task NotifyEntityUpdated(string entityType, string entityId);
    Task NotifyEntityDeleted(string entityType, string entityId);
}
