using System.Net;
using System.Net.Http.Json;
using TestService.Api.Authentication;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

/// <summary>
/// End-to-end tests that an API key (X-Api-Key header) authenticates through the real
/// pipeline and inherits its creator's permissions, alongside the existing JWT scheme.
/// </summary>
[TestFixture]
public class ApiKeyAuthTests : IntegrationTestBase
{
    private async Task<string> CreateApiKeyAsync(string name)
    {
        // The default admin (authenticated by the base class) has settings.api_keys.create.
        var response = await Client.PostAsJsonAsync("/api/settings/api-keys",
            new CreateApiKeyRequest { Name = name });
        AssertStatusCode(response, HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Key, Is.Not.Null.And.Not.Empty, "Created key value should be returned.");
        return created.Key;
    }

    private HttpClient CreateApiKeyClient(string apiKey)
    {
        // A client with no JWT — authentication must come solely from the API key.
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.HeaderName, apiKey);
        return client;
    }

    [Test]
    public async Task ValidApiKey_AuthenticatesProtectedEndpoint()
    {
        var keyValue = await CreateApiKeyAsync("it-info-key");
        using var apiKeyClient = CreateApiKeyClient(keyValue);

        var response = await apiKeyClient.GetAsync("/api/info");

        AssertStatusCode(response, HttpStatusCode.OK);
        var info = await response.Content.ReadFromJsonAsync<AppInfo>();
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.ApiVersion, Is.EqualTo("v1"));
    }

    [Test]
    public async Task ValidApiKey_InheritsCreatorAdminPermissions()
    {
        // Key created by the admin → should reach an admin-only endpoint (users.read).
        var keyValue = await CreateApiKeyAsync("it-admin-key");
        using var apiKeyClient = CreateApiKeyClient(keyValue);

        var response = await apiKeyClient.GetAsync("/api/users");

        AssertStatusCode(response, HttpStatusCode.OK);
    }

    [Test]
    public async Task InvalidApiKey_ReturnsUnauthorized()
    {
        using var apiKeyClient = CreateApiKeyClient("ts_this_is_not_a_real_key");

        var response = await apiKeyClient.GetAsync("/api/info");

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task NoCredential_StillReturnsUnauthorized()
    {
        // Regression: the added API-key scheme must not accidentally allow anonymous access.
        using var anonymousClient = Factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/info");

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task JwtStillWorks_AlongsideApiKeyScheme()
    {
        // The base-class Client uses the admin JWT; adding the API-key scheme must not break it.
        var response = await Client.GetAsync("/api/info");

        AssertStatusCode(response, HttpStatusCode.OK);
    }
}
