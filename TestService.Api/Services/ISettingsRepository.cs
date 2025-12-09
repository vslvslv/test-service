using TestService.Api.Models;

namespace TestService.Api.Services;

public interface ISettingsRepository
{
    // Application Settings
    Task<AppSettings> GetSettingsAsync();
    Task<AppSettings> UpdateSettingsAsync(AppSettings settings);

    // API Keys
    Task<IEnumerable<ApiKey>> GetApiKeysAsync();
    Task<ApiKey?> GetApiKeyByIdAsync(string id);
    Task<ApiKey?> GetApiKeyByValueAsync(string key);
    Task<ApiKey> CreateApiKeyAsync(ApiKey apiKey);
    Task<bool> DeleteApiKeyAsync(string id);
    Task UpdateApiKeyLastUsedAsync(string id);
}
