using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Services;
using TestService.Api.Configuration;

namespace TestService.Api.BackgroundServices;

/// <summary>
/// Background service that automatically cleans up old data based on retention settings
/// </summary>
public class DataCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

    public DataCleanupService(
        IServiceProvider serviceProvider,
        ILogger<DataCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Cleanup Service started");

        // Wait a bit before starting the first cleanup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data cleanup");
            }

            // Wait before next cleanup cycle
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Data Cleanup Service stopped");
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        var schemaRepository = scope.ServiceProvider.GetRequiredService<IEntitySchemaRepository>();
        var mongoSettings = scope.ServiceProvider.GetRequiredService<MongoDbSettings>();

        // Get database connection
        var client = new MongoClient(mongoSettings.ConnectionString);
        var database = client.GetDatabase(mongoSettings.DatabaseName);

        var settings = await settingsRepository.GetSettingsAsync();

        if (!settings.DataRetention.AutoCleanupEnabled)
        {
            _logger.LogDebug("Auto cleanup is disabled, skipping cleanup");
            return;
        }

        _logger.LogInformation("Starting data cleanup cycle");

        // Cleanup expired schemas
        if (settings.DataRetention.SchemaRetentionDays.HasValue)
        {
            await CleanupSchemasAsync(
                schemaRepository, 
                settings.DataRetention.SchemaRetentionDays.Value,
                cancellationToken);
        }

        // Cleanup expired entities
        if (settings.DataRetention.EntityRetentionDays.HasValue)
        {
            await CleanupEntitiesAsync(
                database,
                settings.DataRetention.EntityRetentionDays.Value,
                cancellationToken);
        }

        _logger.LogInformation("Data cleanup cycle completed");
    }

    private async Task CleanupSchemasAsync(
        IEntitySchemaRepository schemaRepository,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var allSchemas = await schemaRepository.GetAllSchemasAsync();
            
            var expiredSchemas = allSchemas
                .Where(s => s.CreatedAt < cutoffDate)
                .ToList();

            if (expiredSchemas.Any())
            {
                _logger.LogInformation(
                    "Found {Count} expired schemas older than {Days} days",
                    expiredSchemas.Count,
                    retentionDays);

                foreach (var schema in expiredSchemas)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await schemaRepository.DeleteSchemaAsync(schema.EntityName);
                    _logger.LogInformation(
                        "Deleted expired schema: {SchemaName} (created: {Created})",
                        schema.EntityName,
                        schema.CreatedAt);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up schemas");
        }
    }

    private async Task CleanupEntitiesAsync(
        IMongoDatabase database,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            
            // Get all collections that might contain entities
            var cursor = await database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
            var collectionNames = await cursor.ToListAsync(cancellationToken);
            
            var entityCollections = collectionNames
                .Where(name => !name.StartsWith("system.") && 
                              name != "EntitySchemas" && 
                              name != "Settings" && 
                              name != "ApiKeys" &&
                              name != "Users" &&
                              name != "Environments")
                .ToList();

            int totalDeleted = 0;

            foreach (var collectionName in entityCollections)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var collection = database.GetCollection<DynamicEntity>(collectionName);
                
                var filter = Builders<DynamicEntity>.Filter.Lt(e => e.CreatedAt, cutoffDate);
                var result = await collection.DeleteManyAsync(filter, cancellationToken);

                if (result.DeletedCount > 0)
                {
                    totalDeleted += (int)result.DeletedCount;
                    _logger.LogInformation(
                        "Deleted {Count} expired entities from {Collection}",
                        result.DeletedCount,
                        collectionName);
                }
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation(
                    "Total entities deleted: {Count} (older than {Days} days)",
                    totalDeleted,
                    retentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up entities");
        }
    }
}
