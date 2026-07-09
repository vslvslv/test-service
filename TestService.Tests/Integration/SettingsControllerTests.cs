using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

[TestFixture]
public class SettingsControllerTests : IntegrationTestBase
{
    [Test]
    public async Task GetSettings_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/settings");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task GetSettings_WithAdminAuth_ReturnsSettings()
    {
        var response = await Client.GetAsync("/api/settings");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var settings = await response.Content.ReadFromJsonAsync<AppSettings>();
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.DataRetention, Is.Not.Null);
    }

    [Test]
    public async Task GetSettings_ReturnsDefaultSettings_WhenNoneExist()
    {
        var response = await Client.GetAsync("/api/settings");
        var settings = await response.Content.ReadFromJsonAsync<AppSettings>();

        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.DataRetention, Is.Not.Null);
        Assert.That(settings.DataRetention.AutoCleanupEnabled, Is.True);
    }

    [Test]
    public async Task UpdateSettings_WithValidData_UpdatesSuccessfully()
    {
        var newSettings = new AppSettings
        {
            DataRetention = new DataRetentionSettings
            {
                SchemaRetentionDays = 90,
                EntityRetentionDays = 60,
                AutoCleanupEnabled = true
            }
        };

        var updateResponse = await Client.PutAsJsonAsync("/api/settings", newSettings);

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var getResponse = await Client.GetAsync("/api/settings");
        var updatedSettings = await getResponse.Content.ReadFromJsonAsync<AppSettings>();
        Assert.That(updatedSettings, Is.Not.Null);
        Assert.That(updatedSettings!.DataRetention.SchemaRetentionDays, Is.EqualTo(90));
        Assert.That(updatedSettings.DataRetention.EntityRetentionDays, Is.EqualTo(60));
        Assert.That(updatedSettings.DataRetention.AutoCleanupEnabled, Is.True);
    }

    [Test]
    public async Task UpdateSettings_WithNullRetention_SetsToNever()
    {
        var settings = new AppSettings
        {
            DataRetention = new DataRetentionSettings
            {
                SchemaRetentionDays = null,
                EntityRetentionDays = null,
                AutoCleanupEnabled = false
            }
        };

        var response = await Client.PutAsJsonAsync("/api/settings", settings);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var getResponse = await Client.GetAsync("/api/settings");
        var updatedSettings = await getResponse.Content.ReadFromJsonAsync<AppSettings>();
        Assert.That(updatedSettings?.DataRetention.SchemaRetentionDays, Is.Null);
        Assert.That(updatedSettings?.DataRetention.EntityRetentionDays, Is.Null);
        Assert.That(updatedSettings?.DataRetention.AutoCleanupEnabled, Is.False);
    }

    [Test]
    public async Task UpdateSettings_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var settings = new AppSettings { DataRetention = new DataRetentionSettings() };

        var response = await Client.PutAsJsonAsync("/api/settings", settings);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
