using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TestService.Api.Authentication;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Unit;

[TestFixture]
public class ApiKeyAuthenticationHandlerTests
{
    private const string KeyValue = "ts_validkey";

    private ISettingsRepository _settings = null!;
    private IUserRepository _users = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = Substitute.For<ISettingsRepository>();
        _users = Substitute.For<IUserRepository>();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<AuthenticateResult> AuthenticateAsync(string? header)
    {
        var options = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Get(Arg.Any<string>()).Returns(new AuthenticationSchemeOptions());

        var handler = new ApiKeyAuthenticationHandler(
            options, NullLoggerFactory.Instance, UrlEncoder.Default, _settings, _users);

        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers[ApiKeyAuthenticationHandler.HeaderName] = header;
        }

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationHandler.SchemeName,
            ApiKeyAuthenticationHandler.SchemeName,
            typeof(ApiKeyAuthenticationHandler));

        await handler.InitializeAsync(scheme, context);
        return await handler.AuthenticateAsync();
    }

    private static ApiKey ValidKey(string? ownerId = "user-admin") => new()
    {
        Id = "key-1",
        Name = "CI key",
        Key = KeyValue,
        CreatedBy = "display-name",       // display/audit only
        CreatedByUserId = ownerId,        // authority is resolved from this
        IsActive = true,
        ExpiresAt = null
    };

    private static User AdminUser() => new()
    {
        Id = "user-admin",
        Username = "admin",
        Email = "admin@test.local",
        Role = UserRole.Admin,
        IsActive = true
    };

    private static User ContributorUser() => new()
    {
        Id = "user-contrib",
        Username = "contrib",
        Email = "contrib@test.local",
        Role = UserRole.Contributor,
        IsActive = true
    };

    private static HashSet<string> PermissionClaims(AuthenticateResult result) =>
        result.Principal!.FindAll("permission").Select(c => c.Value).ToHashSet();

    // ── No credential / malformed ───────────────────────────────────────────────

    [Test]
    public async Task NoHeader_ReturnsNoResult_SoOtherSchemesCanRun()
    {
        var result = await AuthenticateAsync(header: null);

        Assert.That(result.None, Is.True, "Absent X-Api-Key must yield NoResult, not a failure.");
    }

    [Test]
    public async Task EmptyHeader_Fails()
    {
        var result = await AuthenticateAsync(header: "   ");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.None, Is.False, "An empty key is a failure, not NoResult.");
        });
    }

    // ── Key validation ──────────────────────────────────────────────────────────

    [Test]
    public async Task UnknownKey_Fails()
    {
        _settings.GetApiKeyByValueAsync(KeyValue).Returns((ApiKey?)null);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task DisabledKey_Fails()
    {
        var key = ValidKey();
        key.IsActive = false;
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(key);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False);
        await _users.DidNotReceive().GetByIdAsync(Arg.Any<string>());
    }

    [Test]
    public async Task ExpiredKey_Fails()
    {
        var key = ValidKey();
        key.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(key);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task KeyWithoutOwnerId_Fails()
    {
        var key = ValidKey(ownerId: null);
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(key);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False);
        await _users.DidNotReceive().GetByIdAsync(Arg.Any<string>());
    }

    // ── Owner validation ────────────────────────────────────────────────────────

    [Test]
    public async Task OwnerMissing_Fails()
    {
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey());
        _users.GetByIdAsync("user-admin").Returns((User?)null);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task OwnerDisabled_Fails()
    {
        var owner = AdminUser();
        owner.IsActive = false;
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey());
        _users.GetByIdAsync("user-admin").Returns(owner);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False);
    }

    // ── Success + claim inheritance ─────────────────────────────────────────────

    [Test]
    public async Task ValidKey_AdminOwner_GrantsCreatorEffectivePermissions()
    {
        var owner = AdminUser();
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey());
        _users.GetByIdAsync("user-admin").Returns(owner);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.True, result.Failure?.Message);
        Assert.Multiple(() =>
        {
            Assert.That(result.Ticket!.AuthenticationScheme, Is.EqualTo(ApiKeyAuthenticationHandler.SchemeName));
            Assert.That(result.Principal!.FindFirstValue(ClaimTypes.Name), Is.EqualTo("admin"),
                "Requests are attributed to the key's owner.");
            Assert.That(result.Principal!.FindFirstValue("auth_method"), Is.EqualTo("api_key"));
            Assert.That(PermissionClaims(result),
                Is.EquivalentTo(PermissionDefinitions.GetEffectivePermissions(owner)),
                "Permissions must exactly mirror the creating user's effective permissions.");
        });
        // Authority must be resolved by immutable id, never by the (reusable) username.
        await _users.Received(1).GetByIdAsync("user-admin");
        await _users.DidNotReceive().GetByUsernameAsync(Arg.Any<string>());
    }

    [Test]
    public async Task ValidKey_ContributorOwner_GrantsOnlyContributorPermissions()
    {
        var owner = ContributorUser();
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey(ownerId: "user-contrib"));
        _users.GetByIdAsync("user-contrib").Returns(owner);

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.True, result.Failure?.Message);
        var perms = PermissionClaims(result);
        Assert.Multiple(() =>
        {
            Assert.That(perms, Is.EquivalentTo(PermissionDefinitions.GetEffectivePermissions(owner)));
            // A contributor key must NOT inherit admin-only reach.
            Assert.That(perms, Is.Not.SupersetOf(PermissionDefinitions.GetEffectivePermissions(AdminUser())));
            Assert.That(perms, Does.Not.Contain(PermissionDefinitions.UsersRead));
        });
    }

    [Test]
    public async Task ValidKey_UpdatesLastUsed()
    {
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey());
        _users.GetByIdAsync("user-admin").Returns(AdminUser());

        await AuthenticateAsync(KeyValue);

        await _settings.Received(1).UpdateApiKeyLastUsedAsync("key-1");
    }

    [Test]
    public async Task TrimsWhitespaceFromHeaderBeforeLookup()
    {
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey());
        _users.GetByIdAsync("user-admin").Returns(AdminUser());

        var result = await AuthenticateAsync($"  {KeyValue}\n");

        Assert.That(result.Succeeded, Is.True);
        await _settings.Received(1).GetApiKeyByValueAsync(KeyValue);
    }

    // ── Resilience ──────────────────────────────────────────────────────────────

    [Test]
    public async Task RepositoryThrows_FailsClosed()
    {
        _settings.GetApiKeyByValueAsync(KeyValue)
            .Returns(Task.FromException<ApiKey?>(new Exception("db down")));

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.False, "A backing-store error must never authenticate the request.");
    }

    [Test]
    public async Task LastUsedUpdateFailure_DoesNotFailAuthentication()
    {
        _settings.GetApiKeyByValueAsync(KeyValue).Returns(ValidKey());
        _users.GetByIdAsync("user-admin").Returns(AdminUser());
        _settings.UpdateApiKeyLastUsedAsync(Arg.Any<string>())
            .Returns(Task.FromException(new Exception("write failed")));

        var result = await AuthenticateAsync(KeyValue);

        Assert.That(result.Succeeded, Is.True, "LastUsed tracking is best-effort and must not block auth.");
    }
}
