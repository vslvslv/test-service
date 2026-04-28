using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

/// <summary>
/// Tests for health and auth endpoints (bug-fix regression: health used by nginx/compose, login used by frontend).
/// </summary>
[TestFixture]
public class ApiHealthAndAuthTests : IntegrationTestBase
{
    [Test]
    public async Task Health_ReturnsOk_SoNginxAndComposeHealthchecksSucceed()
    {
        var response = await Client.GetAsync("/health");

        AssertStatusCode(response, HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("healthy").Or.Contain("OK"),
            "Health response body should indicate healthy (used by Docker/K8s healthchecks)");
    }

    [Test]
    public async Task AuthLogin_EmptyBody_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", (LoginRequest?)null);

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AuthLogin_EmptyUsername_ReturnsBadRequest()
    {
        var request = new LoginRequest { Username = "", Password = "any" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", request);

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AuthLogin_InvalidCredentials_ReturnsUnauthorized()
    {
        var request = new LoginRequest { Username = "nonexistent", Password = "wrong" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", request);

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task AuthLogin_ValidDefaultAdmin_ReturnsOkAndToken()
    {
        var request = new LoginRequest { Username = "admin", Password = "Admin@123" };
        var response = await Client.PostAsJsonAsync("/api/auth/login", request);

        AssertStatusCode(response, HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(loginResponse, Is.Not.Null);
        Assert.That(loginResponse!.Token, Is.Not.Empty);
        Assert.That(loginResponse.Username, Is.EqualTo("admin"));
    }
}
