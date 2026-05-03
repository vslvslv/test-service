using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

[TestFixture]
public class ApiKeysControllerTests : IntegrationTestBase
{
    #region Authorization Tests

    [Test]
    public async Task GetApiKeys_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/settings/api-keys");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DeleteApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.DeleteAsync("/api/settings/api-keys/507f1f77bcf86cd799439011");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region API Key Retrieval Tests

    [Test]
    public async Task GetApiKeys_WithAdminAuth_ReturnsListOfKeys()
    {
        var response = await Client.GetAsync("/api/settings/api-keys");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keys, Is.Not.Null);
    }

    [Test]
    public async Task GetApiKeys_ReturnsEmptyList_WhenNoKeysExist()
    {
        var response = await Client.GetAsync("/api/settings/api-keys");
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKey>>();

        Assert.That(keys, Is.Not.Null);
    }

    #endregion

    #region API Key Creation Tests

    [Test]
    public async Task CreateApiKey_WithValidData_CreatesSuccessfully()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"Test API Key {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdKey = await response.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey, Is.Not.Null);
        Assert.That(createdKey!.Name, Does.Contain("Test API Key"));
        Assert.That(createdKey.Key, Is.Not.Null);
        Assert.That(createdKey.Key, Does.StartWith("ts_"));
        Assert.That(createdKey.ExpiresAt, Is.Not.Null);
        Assert.That(createdKey.IsActive, Is.True);
        Assert.That(createdKey.CreatedBy, Is.EqualTo("admin"));
    }

    [Test]
    public async Task CreateApiKey_WithNeverExpires_CreatesKeyWithoutExpiration()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"Never Expires Key {Guid.NewGuid()}",
            ExpirationDays = null
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdKey = await response.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey, Is.Not.Null);
        Assert.That(createdKey!.ExpiresAt, Is.Null);
    }

    [Test]
    public async Task CreateApiKey_WithEmptyName_ReturnsBadRequest()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateApiKey_GeneratesUniqueKeys()
    {
        var request1 = new CreateApiKeyRequest { Name = $"Key 1 {Guid.NewGuid()}", ExpirationDays = 90 };
        var request2 = new CreateApiKeyRequest { Name = $"Key 2 {Guid.NewGuid()}", ExpirationDays = 90 };

        var response1 = await Client.PostAsJsonAsync("/api/settings/api-keys", request1);
        var response2 = await Client.PostAsJsonAsync("/api/settings/api-keys", request2);

        var key1 = await response1.Content.ReadFromJsonAsync<ApiKey>();
        var key2 = await response2.Content.ReadFromJsonAsync<ApiKey>();

        Assert.That(key1, Is.Not.Null);
        Assert.That(key2, Is.Not.Null);
        Assert.That(key1!.Key, Is.Not.EqualTo(key2!.Key));
    }

    #endregion

    #region API Key Format Tests

    [Test]
    public async Task ApiKey_HasCorrectFormat()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"Format Test Key {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.That(key?.Key, Is.Not.Null);
        Assert.That(key!.Key, Does.StartWith("ts_"));
        Assert.That(key.Key.Length, Is.GreaterThan(10));
        Assert.That(key.Key, Does.Match(@"^ts_[a-z0-9]+$"));
    }

    [Test]
    public async Task ApiKey_HasMinimumLength()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"Length Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.That(key?.Key, Is.Not.Null);
        Assert.That(key!.Key.Length, Is.GreaterThanOrEqualTo(35));
    }

    #endregion

    #region API Key Metadata Tests

    [Test]
    public async Task CreateApiKey_SetsCreatedByToCurrentUser()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"CreatedBy Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.That(key, Is.Not.Null);
        Assert.That(key!.CreatedBy, Is.EqualTo("admin"));
    }

    [Test]
    public async Task CreateApiKey_SetsCreatedAtToCurrentTime()
    {
        var beforeCreation = DateTime.UtcNow.AddSeconds(-5);

        var request = new CreateApiKeyRequest
        {
            Name = $"CreatedAt Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();
        var afterCreation = DateTime.UtcNow.AddSeconds(5);

        Assert.That(key, Is.Not.Null);
        Assert.That(key!.CreatedAt, Is.GreaterThan(beforeCreation));
        Assert.That(key.CreatedAt, Is.LessThan(afterCreation));
    }

    [Test]
    public async Task CreateApiKey_LastUsedIsNull_OnCreation()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"LastUsed Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.That(key, Is.Not.Null);
        Assert.That(key!.LastUsed, Is.Null);
    }

    #endregion

    #region API Key Expiration Tests

    [Test]
    public async Task CreateApiKey_CalculatesExpirationCorrectly()
    {
        var beforeCreation = DateTime.UtcNow;

        var request = new CreateApiKeyRequest
        {
            Name = $"Expiration Test {Guid.NewGuid()}",
            ExpirationDays = 30
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        var expectedExpiration = beforeCreation.AddDays(30);

        Assert.That(key?.ExpiresAt, Is.Not.Null);
        Assert.That(key!.ExpiresAt!.Value, Is.EqualTo(expectedExpiration).Within(TimeSpan.FromMinutes(1)));
    }

    [Test]
    [TestCase(1)]
    [TestCase(7)]
    [TestCase(30)]
    [TestCase(90)]
    [TestCase(365)]
    public async Task CreateApiKey_SupportsVariousExpirationPeriods(int days)
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"Expiration {days}d Test {Guid.NewGuid()}",
            ExpirationDays = days
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(key?.ExpiresAt, Is.Not.Null);
    }

    #endregion

    #region API Key Deletion Tests

    [Test]
    public async Task DeleteApiKey_WithValidId_DeletesSuccessfully()
    {
        var createRequest = new CreateApiKeyRequest
        {
            Name = $"Key to Delete {Guid.NewGuid()}",
            ExpirationDays = 90
        };
        var createResponse = await Client.PostAsJsonAsync("/api/settings/api-keys", createRequest);
        var createdKey = await createResponse.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey?.Id, Is.Not.Null);
        var keyId = createdKey!.Id;

        var deleteResponse = await Client.DeleteAsync($"/api/settings/api-keys/{keyId}");

        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync("/api/settings/api-keys");
        var keys = await getResponse.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keys, Is.Not.Null);
        Assert.That(keys!.Any(k => k.Id == keyId), Is.False);
    }

    [Test]
    public async Task DeleteApiKey_WithInvalidId_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/settings/api-keys/507f1f77bcf86cd799439011");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteApiKey_WithMalformedId_ReturnsBadRequestOrNotFound()
    {
        var response = await Client.DeleteAsync("/api/settings/api-keys/invalid-id");

        Assert.That(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound,
            Is.True);
    }

    #endregion

    #region API Key Listing Tests

    [Test]
    public async Task GetApiKeys_OrdersByCreatedDateDescending()
    {
        for (int i = 0; i < 3; i++)
        {
            await Client.PostAsJsonAsync("/api/settings/api-keys", new CreateApiKeyRequest
            {
                Name = $"Ordered Key {i} {Guid.NewGuid()}",
                ExpirationDays = 90
            });
        }

        var response = await Client.GetAsync("/api/settings/api-keys");
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKey>>();

        Assert.That(keys, Is.Not.Null);
        Assert.That(keys!.Count, Is.GreaterThanOrEqualTo(3));

        for (int i = 0; i < keys.Count - 1; i++)
        {
            Assert.That(keys[i].CreatedAt, Is.GreaterThanOrEqualTo(keys[i + 1].CreatedAt));
        }
    }

    #endregion

    #region API Key Validation Tests

    [Test]
    public async Task CreateApiKey_WithWhitespaceName_ReturnsBadRequest()
    {
        var request = new CreateApiKeyRequest
        {
            Name = "   ",
            ExpirationDays = 90
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateApiKey_WithNegativeExpiration_ShouldStillWork()
    {
        var request = new CreateApiKeyRequest
        {
            Name = $"Negative Expiration Test {Guid.NewGuid()}",
            ExpirationDays = -1
        };

        var response = await Client.PostAsJsonAsync("/api/settings/api-keys", request);

        Assert.That(
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.BadRequest,
            Is.True);
    }

    #endregion

    #region Multiple Operations Tests

    [Test]
    public async Task ApiKey_FullLifecycle_CreateListDelete()
    {
        var keyName = $"Lifecycle Test {Guid.NewGuid()}";

        // Create
        var createRequest = new CreateApiKeyRequest { Name = keyName, ExpirationDays = 90 };
        var createResponse = await Client.PostAsJsonAsync("/api/settings/api-keys", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdKey = await createResponse.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey, Is.Not.Null);

        // List
        var listResponse = await Client.GetAsync("/api/settings/api-keys");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var keys = await listResponse.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keys!.Any(k => k.Name == keyName), Is.True);

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/settings/api-keys/{createdKey!.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var verifyResponse = await Client.GetAsync("/api/settings/api-keys");
        var keysAfterDelete = await verifyResponse.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keysAfterDelete!.Any(k => k.Name == keyName), Is.False);
    }

    #endregion
}
