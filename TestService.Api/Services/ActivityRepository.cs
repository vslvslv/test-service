using MongoDB.Driver;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IActivityRepository
{
    Task<Activity> CreateAsync(Activity activity);
    Task<List<Activity>> GetRecentAsync(int days = 7, int limit = 100);
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
    Task<int> DeleteOldActivitiesAsync(int daysToKeep = 7);
}

public class ActivityRepository : IActivityRepository
{
    private readonly IMongoCollection<Activity> _activities;

    public ActivityRepository(IMongoDatabase database)
    {
        _activities = database.GetCollection<Activity>("activities");
        
        // Create indexes for better query performance
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Index on timestamp for date range queries and cleanup
        var timestampIndexModel = new CreateIndexModel<Activity>(
            Builders<Activity>.IndexKeys.Descending(a => a.Timestamp),
            new CreateIndexOptions { Name = "timestamp_desc" }
        );

        // Index on entity type for filtering
        var entityTypeIndexModel = new CreateIndexModel<Activity>(
            Builders<Activity>.IndexKeys.Ascending(a => a.EntityType),
            new CreateIndexOptions { Name = "entityType_asc" }
        );

        // Compound index for common query patterns
        var compoundIndexModel = new CreateIndexModel<Activity>(
            Builders<Activity>.IndexKeys
                .Descending(a => a.Timestamp)
                .Ascending(a => a.EntityType)
                .Ascending(a => a.Type),
            new CreateIndexOptions { Name = "timestamp_entityType_type" }
        );

        _activities.Indexes.CreateMany(new[] 
        { 
            timestampIndexModel, 
            entityTypeIndexModel, 
            compoundIndexModel 
        });
    }

    public async Task<Activity> CreateAsync(Activity activity)
    {
        activity.Timestamp = DateTime.UtcNow;
        await _activities.InsertOneAsync(activity);
        return activity;
    }

    public async Task<List<Activity>> GetRecentAsync(int days = 7, int limit = 100)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        
        return await _activities
            .Find(a => a.Timestamp >= startDate)
            .SortByDescending(a => a.Timestamp)
            .Limit(limit)
            .ToListAsync();
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
        var filterBuilder = Builders<Activity>.Filter;
        var filters = new List<FilterDefinition<Activity>>();

        // Default to last 7 days if no date range specified
        if (!startDate.HasValue && !endDate.HasValue)
        {
            startDate = DateTime.UtcNow.AddDays(-7);
        }

        if (startDate.HasValue)
        {
            filters.Add(filterBuilder.Gte(a => a.Timestamp, startDate.Value));
        }

        if (endDate.HasValue)
        {
            filters.Add(filterBuilder.Lte(a => a.Timestamp, endDate.Value));
        }

        if (!string.IsNullOrEmpty(entityType))
        {
            filters.Add(filterBuilder.Eq(a => a.EntityType, entityType));
        }

        if (!string.IsNullOrEmpty(type))
        {
            filters.Add(filterBuilder.Eq(a => a.Type, type));
        }

        if (!string.IsNullOrEmpty(action))
        {
            filters.Add(filterBuilder.Eq(a => a.Action, action));
        }

        if (!string.IsNullOrEmpty(user))
        {
            filters.Add(filterBuilder.Eq(a => a.User, user));
        }

        var combinedFilter = filters.Count > 0
            ? filterBuilder.And(filters)
            : filterBuilder.Empty;

        return await _activities
            .Find(combinedFilter)
            .SortByDescending(a => a.Timestamp)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var filterBuilder = Builders<Activity>.Filter;
        var filters = new List<FilterDefinition<Activity>>();

        if (startDate.HasValue)
        {
            filters.Add(filterBuilder.Gte(a => a.Timestamp, startDate.Value));
        }

        if (endDate.HasValue)
        {
            filters.Add(filterBuilder.Lte(a => a.Timestamp, endDate.Value));
        }

        var combinedFilter = filters.Count > 0
            ? filterBuilder.And(filters)
            : filterBuilder.Empty;

        return (int)await _activities.CountDocumentsAsync(combinedFilter);
    }

    public async Task<int> DeleteOldActivitiesAsync(int daysToKeep = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        var filter = Builders<Activity>.Filter.Lt(a => a.Timestamp, cutoffDate);
        
        var result = await _activities.DeleteManyAsync(filter);
        return (int)result.DeletedCount;
    }
}
