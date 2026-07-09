using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Api.Services;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

/// <summary>
/// Tests for the version/info endpoint that backs the UI About dialog.
/// </summary>
[TestFixture]
public class InfoControllerTests : IntegrationTestBase
{
    [Test]
    public async Task GetInfo_WithoutAuth_ReturnsUnauthorized()
    {
        // Base class authenticates as admin by default; drop the token for this test.
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/info");

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetInfo_Authenticated_ReturnsPopulatedInfo()
    {
        var response = await Client.GetAsync("/api/info");

        AssertStatusCode(response, HttpStatusCode.OK);
        var info = await response.Content.ReadFromJsonAsync<AppInfo>();

        Assert.That(info, Is.Not.Null, $"Info payload should deserialize. Content: {await response.Content.ReadAsStringAsync()}");
        Assert.Multiple(() =>
        {
            Assert.That(info!.Name, Is.EqualTo("Test Service API"), "Name should be the application name.");
            Assert.That(info.Version, Is.Not.Null.And.Not.Empty, "Version should be populated.");
            Assert.That(info.ApiVersion, Is.EqualTo(AppInfoService.ApiVersion), "API version should match the service constant.");
            Assert.That(info.Environment, Is.Not.Null.And.Not.Empty, "Environment name should be populated.");
            Assert.That(info.Runtime, Does.Contain(".NET"), "Runtime should report the .NET framework description.");
            Assert.That(info.Commit, Is.Not.Null.And.Not.Empty, "Commit should be populated (or 'dev').");
            Assert.That(info.UptimeSeconds, Is.GreaterThanOrEqualTo(0), "Uptime should be non-negative.");
            Assert.That(info.ServerTimeUtc, Does.EndWith("Z"), "Server time should be an ISO-8601 UTC string.");
        });
    }
}
