namespace TestService.Api.Services;

/// <summary>
/// One-time migration that populates <c>CreatedByUserId</c> for legacy API keys created
/// before owner-id tracking existed (they only recorded the creator's username).
/// </summary>
public interface IApiKeyOwnerBackfillService
{
    /// <summary>Backfills missing owner ids and returns the number of keys updated.</summary>
    Task<int> RunAsync();
}

/// <summary>
/// Resolves each legacy key's <c>CreatedBy</c> username to the current immutable user id
/// and pins it. This runs once, at startup, in a controlled context — so unlike resolving
/// by username on every request, later deletion/reuse of a username cannot hijack a key
/// (authentication resolves strictly by the pinned id). Idempotent: keys that already have
/// an owner id, have no creator username, or whose creator no longer exists are skipped.
/// </summary>
public sealed class ApiKeyOwnerBackfillService : IApiKeyOwnerBackfillService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ApiKeyOwnerBackfillService> _logger;

    public ApiKeyOwnerBackfillService(
        ISettingsRepository settingsRepository,
        IUserRepository userRepository,
        ILogger<ApiKeyOwnerBackfillService> logger)
    {
        _settingsRepository = settingsRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<int> RunAsync()
    {
        var keys = await _settingsRepository.GetApiKeysAsync();
        var updated = 0;

        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key.CreatedByUserId)
                || string.IsNullOrWhiteSpace(key.CreatedBy)
                || string.IsNullOrWhiteSpace(key.Id))
            {
                continue;
            }

            var owner = await _userRepository.GetByUsernameAsync(key.CreatedBy);
            if (string.IsNullOrWhiteSpace(owner?.Id))
            {
                _logger.LogWarning(
                    "API key {KeyId} references unknown creator '{Username}'; leaving owner id unset.",
                    key.Id, key.CreatedBy);
                continue;
            }

            await _settingsRepository.UpdateApiKeyOwnerIdAsync(key.Id, owner.Id);
            updated++;
        }

        if (updated > 0)
        {
            _logger.LogInformation("Backfilled owner id for {Count} legacy API key(s).", updated);
        }

        return updated;
    }
}
