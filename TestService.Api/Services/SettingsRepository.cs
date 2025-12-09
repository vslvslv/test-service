using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public class SettingsRepository : ISettingsRepository
{
    private readonly IMongoCollection<AppSettings> _settingsCollection;
    private readonly IMongoCollection<ApiKey> _apiKeysCollection;
    private readonly ILogger<SettingsRepository> _logger;
    private const string SETTINGS_ID = "app_settings"; // Singleton settings document

    public SettingsRepository(
        MongoDbSettings settings,
        ILogger<SettingsRepository> logger)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _settingsCollection = database.GetCollection<AppSettings>("Settings");
        _apiKeysCollection = database.GetCollection<ApiKey>("ApiKeys");
        _logger = logger;
    }

    #region Application Settings

    public async Task<AppSettings> GetSettingsAsync()
    {
        try
        {
            var settings = await _settingsCollection
                .Find(s => s.Id == SETTINGS_ID)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                // Create default settings if none exist
                settings = new AppSettings
                {
                    Id = SETTINGS_ID,
                    DataRetention = new DataRetentionSettings
                    {
                        SchemaRetentionDays = null, // Never delete by default
                        EntityRetentionDays = 30,   // 30 days default
                        AutoCleanupEnabled = true
                    }
                };

                await _settingsCollection.InsertOneAsync(settings);
                _logger.LogInformation("Created default application settings");
            }

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting application settings");
            throw;
        }
    }

    public async Task<AppSettings> UpdateSettingsAsync(AppSettings settings)
    {
        try
        {
            settings.Id = SETTINGS_ID;
            settings.UpdatedAt = DateTime.UtcNow;

            var result = await _settingsCollection.ReplaceOneAsync(
                s => s.Id == SETTINGS_ID,
                settings,
                new ReplaceOptions { IsUpsert = true }
            );

            _logger.LogInformation("Updated application settings");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating application settings");
            throw;
        }
    }

    #endregion

    #region API Keys

    public async Task<IEnumerable<ApiKey>> GetApiKeysAsync()
    {
        try
        {
            return await _apiKeysCollection
                .Find(_ => true)
                .SortByDescending(k => k.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API keys");
            throw;
        }
    }

    public async Task<ApiKey?> GetApiKeyByIdAsync(string id)
    {
        try
        {
            return await _apiKeysCollection
                .Find(k => k.Id == id)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key by ID: {Id}", id);
            throw;
        }
    }

    public async Task<ApiKey?> GetApiKeyByValueAsync(string key)
    {
        try
        {
            // In production, you should hash the key and compare hashes
            return await _apiKeysCollection
                .Find(k => k.Key == key && k.IsActive)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key by value");
            throw;
        }
    }

    public async Task<ApiKey> CreateApiKeyAsync(ApiKey apiKey)
    {
        try
        {
            apiKey.CreatedAt = DateTime.UtcNow;
            apiKey.IsActive = true;

            await _apiKeysCollection.InsertOneAsync(apiKey);
            
            _logger.LogInformation("Created API key: {Name}", apiKey.Name);
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating API key");
            throw;
        }
    }

    public async Task<bool> DeleteApiKeyAsync(string id)
    {
        try
        {
            // Validate ObjectId format before querying
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                _logger.LogWarning("Invalid ObjectId format for deletion: {Id}", id);
                return false;
            }

            var result = await _apiKeysCollection.DeleteOneAsync(k => k.Id == id);
            
            if (result.DeletedCount > 0)
            {
                _logger.LogInformation("Deleted API key: {Id}", id);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting API key: {Id}", id);
            throw;
        }
    }

    public async Task UpdateApiKeyLastUsedAsync(string id)
    {
        try
        {
            var update = Builders<ApiKey>.Update.Set(k => k.LastUsed, DateTime.UtcNow);
            await _apiKeysCollection.UpdateOneAsync(k => k.Id == id, update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating API key last used: {Id}", id);
            // Don't throw - this is not critical
        }
    }

    #endregion
}
