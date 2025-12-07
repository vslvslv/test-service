using MongoDB.Bson;
using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IEnvironmentService
{
    Task<EnvironmentResponse?> GetByIdAsync(string id, bool includeStatistics = false);
    Task<EnvironmentResponse?> GetByNameAsync(string name, bool includeStatistics = false);
    Task<IEnumerable<EnvironmentResponse>> GetAllAsync(bool includeInactive = false, bool includeStatistics = false);
    Task<EnvironmentResponse> CreateAsync(CreateEnvironmentRequest request, string? createdBy = null);
    Task<bool> UpdateAsync(string id, UpdateEnvironmentRequest request);
    Task<bool> DeleteAsync(string id);
    Task<EnvironmentStatistics> GetStatisticsAsync(string environmentName);
    Task InitializeDefaultEnvironmentsAsync();
}

public class EnvironmentService : IEnvironmentService
{
    private readonly IEnvironmentRepository _repository;
    private readonly IMongoDatabase _database;
    private readonly ILogger<EnvironmentService> _logger;

    public EnvironmentService(
        IEnvironmentRepository repository,
        MongoDbSettings mongoDbSettings,
        ILogger<EnvironmentService> logger)
    {
        _repository = repository;
        _logger = logger;
        
        var client = new MongoClient(mongoDbSettings.ConnectionString);
        _database = client.GetDatabase(mongoDbSettings.DatabaseName);
    }

    public async Task<EnvironmentResponse?> GetByIdAsync(string id, bool includeStatistics = false)
    {
        var environment = await _repository.GetByIdAsync(id);
        if (environment == null)
        {
            return null;
        }

        EnvironmentStatistics? stats = null;
        if (includeStatistics)
        {
            stats = await GetStatisticsAsync(environment.Name);
        }

        return EnvironmentResponse.FromEnvironment(environment, stats);
    }

    public async Task<EnvironmentResponse?> GetByNameAsync(string name, bool includeStatistics = false)
    {
        var environment = await _repository.GetByNameAsync(name);
        if (environment == null)
        {
            return null;
        }

        EnvironmentStatistics? stats = null;
        if (includeStatistics)
        {
            stats = await GetStatisticsAsync(environment.Name);
        }

        return EnvironmentResponse.FromEnvironment(environment, stats);
    }

    public async Task<IEnumerable<EnvironmentResponse>> GetAllAsync(bool includeInactive = false, bool includeStatistics = false)
    {
        var environments = await _repository.GetAllAsync(includeInactive);
        var responses = new List<EnvironmentResponse>();

        foreach (var env in environments)
        {
            EnvironmentStatistics? stats = null;
            if (includeStatistics)
            {
                stats = await GetStatisticsAsync(env.Name);
            }

            responses.Add(EnvironmentResponse.FromEnvironment(env, stats));
        }

        return responses;
    }

    public async Task<EnvironmentResponse> CreateAsync(CreateEnvironmentRequest request, string? createdBy = null)
    {
        // Validate name uniqueness
        if (await _repository.NameExistsAsync(request.Name))
        {
            throw new InvalidOperationException($"Environment '{request.Name}' already exists");
        }

        // Validate name format (lowercase, alphanumeric, hyphens)
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Name, "^[a-z0-9-]+$"))
        {
            throw new ArgumentException("Environment name must be lowercase alphanumeric with hyphens only");
        }

        var environment = new Models.Environment
        {
            Name = request.Name.ToLowerInvariant(),
            DisplayName = request.DisplayName,
            Description = request.Description,
            Url = request.Url,
            Color = request.Color,
            Order = request.Order,
            Configuration = request.Configuration ?? new Dictionary<string, string>(),
            Tags = request.Tags ?? new List<string>(),
            IsActive = true,
            CreatedBy = createdBy
        };

        var created = await _repository.CreateAsync(environment);
        _logger.LogInformation("Environment created: {Name} by {User}", created.Name, createdBy ?? "system");

        return EnvironmentResponse.FromEnvironment(created);
    }

    public async Task<bool> UpdateAsync(string id, UpdateEnvironmentRequest request)
    {
        var environment = await _repository.GetByIdAsync(id);
        if (environment == null)
        {
            return false;
        }

        if (request.DisplayName != null) environment.DisplayName = request.DisplayName;
        if (request.Description != null) environment.Description = request.Description;
        if (request.Url != null) environment.Url = request.Url;
        if (request.Color != null) environment.Color = request.Color;
        if (request.IsActive.HasValue) environment.IsActive = request.IsActive.Value;
        if (request.Order.HasValue) environment.Order = request.Order.Value;
        if (request.Configuration != null) environment.Configuration = request.Configuration;
        if (request.Tags != null) environment.Tags = request.Tags;

        var result = await _repository.UpdateAsync(id, environment);
        
        if (result)
        {
            _logger.LogInformation("Environment updated: {Name}", environment.Name);
        }

        return result;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var environment = await _repository.GetByIdAsync(id);
        if (environment == null)
        {
            return false;
        }

        // Check if there are entities in this environment
        var stats = await GetStatisticsAsync(environment.Name);
        if (stats.TotalEntities > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete environment '{environment.Name}' because it contains {stats.TotalEntities} entities. " +
                "Delete all entities first or use force delete.");
        }

        var result = await _repository.DeleteAsync(id);
        
        if (result)
        {
            _logger.LogInformation("Environment deleted: {Name}", environment.Name);
        }

        return result;
    }

    public async Task<EnvironmentStatistics> GetStatisticsAsync(string environmentName)
    {
        var stats = new EnvironmentStatistics
        {
            EntitiesByType = new Dictionary<string, int>()
        };

        try
        {
            // Get all dynamic entity collections
            var collectionNames = await _database.ListCollectionNamesAsync();
            var collections = await collectionNames.ToListAsync();
            var dynamicCollections = collections.Where(c => c.StartsWith("Dynamic_")).ToList();

            int totalEntities = 0;
            int availableEntities = 0;
            int consumedEntities = 0;
            DateTime? lastActivity = null;

            foreach (var collectionName in dynamicCollections)
            {
                var collection = _database.GetCollection<BsonDocument>(collectionName);
                
                // Filter by environment
                var envFilter = Builders<BsonDocument>.Filter.Eq("environment", environmentName);
                
                // Total count
                var total = await collection.CountDocumentsAsync(envFilter);
                if (total == 0) continue;

                totalEntities += (int)total;

                // Available (not consumed)
                var availableFilter = Builders<BsonDocument>.Filter.And(
                    envFilter,
                    Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("isConsumed", false),
                        Builders<BsonDocument>.Filter.Exists("isConsumed", false)
                    )
                );
                var available = await collection.CountDocumentsAsync(availableFilter);
                availableEntities += (int)available;

                // Consumed
                var consumedFilter = Builders<BsonDocument>.Filter.And(
                    envFilter,
                    Builders<BsonDocument>.Filter.Eq("isConsumed", true)
                );
                var consumed = await collection.CountDocumentsAsync(consumedFilter);
                consumedEntities += (int)consumed;

                // Entity type
                var entityType = collectionName.Replace("Dynamic_", "");
                stats.EntitiesByType[entityType] = (int)total;

                // Last activity (most recent updatedAt)
                var sort = Builders<BsonDocument>.Sort.Descending("updatedAt");
                var latestDoc = await collection.Find(envFilter).Sort(sort).Limit(1).FirstOrDefaultAsync();
                if (latestDoc != null && latestDoc.Contains("updatedAt"))
                {
                    var docUpdated = latestDoc["updatedAt"].ToUniversalTime();
                    if (lastActivity == null || docUpdated > lastActivity)
                    {
                        lastActivity = docUpdated;
                    }
                }
            }

            stats.TotalEntities = totalEntities;
            stats.AvailableEntities = availableEntities;
            stats.ConsumedEntities = consumedEntities;
            stats.LastActivity = lastActivity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating statistics for environment: {Environment}", environmentName);
        }

        return stats;
    }

    public async Task InitializeDefaultEnvironmentsAsync()
    {
        // Check if any environments exist
        var environments = await _repository.GetAllAsync(includeInactive: true);
        if (environments.Any())
        {
            _logger.LogInformation("Environments already exist, skipping default environment creation");
            return;
        }

        // Create default environments
        var defaultEnvironments = new[]
        {
            new CreateEnvironmentRequest
            {
                Name = "dev",
                DisplayName = "Development",
                Description = "Development environment for testing new features",
                Color = "#00ff00",
                Order = 1,
                Tags = new List<string> { "development", "testing" }
            },
            new CreateEnvironmentRequest
            {
                Name = "staging",
                DisplayName = "Staging",
                Description = "Staging environment for pre-production testing",
                Color = "#ffa500",
                Order = 2,
                Tags = new List<string> { "staging", "pre-prod" }
            },
            new CreateEnvironmentRequest
            {
                Name = "production",
                DisplayName = "Production",
                Description = "Production environment",
                Color = "#ff0000",
                Order = 3,
                Tags = new List<string> { "production", "live" }
            }
        };

        foreach (var envRequest in defaultEnvironments)
        {
            try
            {
                await CreateAsync(envRequest, "system");
                _logger.LogInformation("Default environment created: {Name}", envRequest.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create default environment: {Name}", envRequest.Name);
            }
        }
    }
}
