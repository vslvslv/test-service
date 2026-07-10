using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Authentication;

/// <summary>
/// Authenticates requests presenting an API key in the <c>X-Api-Key</c> header.
///
/// A valid key resolves to the user that created it (by immutable
/// <see cref="ApiKey.CreatedByUserId"/>), and the request is granted that user's effective
/// permissions — so an API key can do exactly what its creator can, no more. Keys that are
/// unknown, disabled, expired, or whose owner no longer exists / is disabled are rejected.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>Scheme that validates the API key itself.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>
    /// Composite (policy) scheme registered as the default: routes each request to the
    /// API-key or JWT handler based on the presence of the <see cref="HeaderName"/> header.
    /// </summary>
    public const string CompositeSchemeName = "JwtOrApiKey";

    /// <summary>Request header carrying the API key.</summary>
    public const string HeaderName = "X-Api-Key";

    private readonly ISettingsRepository _settingsRepository;
    private readonly IUserRepository _userRepository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISettingsRepository settingsRepository,
        IUserRepository userRepository)
        : base(options, logger, encoder)
    {
        _settingsRepository = settingsRepository;
        _userRepository = userRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            // No API key presented — let another scheme (JWT) handle it.
            return AuthenticateResult.NoResult();
        }

        var providedKey = headerValues.ToString().Trim();
        if (string.IsNullOrEmpty(providedKey))
        {
            return AuthenticateResult.Fail("API key was empty.");
        }

        ApiKey? apiKey;
        User? owner;
        try
        {
            apiKey = await _settingsRepository.GetApiKeyByValueAsync(providedKey);

            // Generic failure messages: never reveal whether a key exists, is disabled,
            // or is expired to the caller (the framework returns a bare 401 regardless).
            if (apiKey is null)
            {
                return AuthenticateResult.Fail("Invalid API key.");
            }
            if (!apiKey.IsActive)
            {
                return AuthenticateResult.Fail("API key is disabled.");
            }
            if (apiKey.IsExpired)
            {
                return AuthenticateResult.Fail("API key has expired.");
            }
            if (string.IsNullOrWhiteSpace(apiKey.CreatedByUserId))
            {
                return AuthenticateResult.Fail("API key has no associated user.");
            }

            // Resolve by the immutable user id, never the username (which can be
            // deleted and later reused by a different account).
            owner = await _userRepository.GetByIdAsync(apiKey.CreatedByUserId);
            if (owner is null)
            {
                return AuthenticateResult.Fail("API key owner no longer exists.");
            }
            if (!owner.IsActive)
            {
                return AuthenticateResult.Fail("API key owner is disabled.");
            }
        }
        catch (Exception ex)
        {
            // A backing-store failure must not authenticate the request.
            Logger.LogError(ex, "API key authentication failed due to an error resolving the key.");
            return AuthenticateResult.Fail("API key could not be validated.");
        }

        var principal = BuildPrincipal(apiKey, owner);

        // Best-effort last-used tracking; never fail the request if it can't be recorded.
        await TryUpdateLastUsedAsync(apiKey);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private ClaimsPrincipal BuildPrincipal(ApiKey apiKey, User owner)
    {
        // Mirror the JWT principal (see TokenService.GenerateToken) so authorization
        // policies behave identically whether the caller used a JWT or an API key.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.Id ?? string.Empty),
            new(ClaimTypes.Name, owner.Username),
            new(ClaimTypes.Email, owner.Email),
            new(ClaimTypes.Role, owner.Role.ToString()),
            new("userId", owner.Id ?? string.Empty),
            new("auth_method", "api_key"),
            new("api_key_id", apiKey.Id ?? string.Empty)
        };

        foreach (var permission in PermissionDefinitions.GetEffectivePermissions(owner))
        {
            claims.Add(new Claim("permission", permission));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return new ClaimsPrincipal(identity);
    }

    private async Task TryUpdateLastUsedAsync(ApiKey apiKey)
    {
        if (string.IsNullOrEmpty(apiKey.Id))
        {
            return;
        }

        try
        {
            await _settingsRepository.UpdateApiKeyLastUsedAsync(apiKey.Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to update LastUsed for API key {ApiKeyId}", apiKey.Id);
        }
    }
}
