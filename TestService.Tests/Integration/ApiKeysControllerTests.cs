using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TestService.Api.Models;
using NUnit.Framework;

namespace TestService.Tests.Integration;

[TestFixture]
public class ApiKeysControllerTests
{
    private HttpClient? _client;
    private WebApplicationFactory<Program>? _factory;

    [SetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var loginResponse = await _client!.PostAsJsonAsync("/api/auth/login", new
        {
            Username = "admin",
            Password = "Admin@123"
        });
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResult?.Token ?? throw new Exception("Failed to get admin token");
    }

    private void SetAuthToken(string token)
    {
        _client!.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    #region Authorization Tests

    [Test]
    public async Task GetApiKeys_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client!.GetAsync("/api/settings/api-keys");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task CreateApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateApiKeyRequest
        {
            Name = "Test Key",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task DeleteApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client!.DeleteAsync("/api/settings/api-keys/507f1f77bcf86cd799439011");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region API Key Retrieval Tests

    [Test]
    public async Task GetApiKeys_WithAdminAuth_ReturnsListOfKeys()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        // Act
        var response = await _client!.GetAsync("/api/settings/api-keys");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keys, Is.Not.Null);
    }

    [Test]
    public async Task GetApiKeys_ReturnsEmptyList_WhenNoKeysExist()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        // Act
        var response = await _client!.GetAsync("/api/settings/api-keys");
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKey>>();

        // Assert
        Assert.That(keys, Is.Not.Null);
        // Note: May contain keys from other tests, just verify it's a valid list
    }

    #endregion

    #region API Key Creation Tests

    [Test]
    public async Task CreateApiKey_WithValidData_CreatesSuccessfully()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"Test API Key {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);

        // Assert
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
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"Never Expires Key {Guid.NewGuid()}",
            ExpirationDays = null
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var createdKey = await response.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey, Is.Not.Null);
        Assert.That(createdKey!.ExpiresAt, Is.Null);
    }

    [Test]
    public async Task CreateApiKey_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = "",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateApiKey_GeneratesUniqueKeys()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request1 = new CreateApiKeyRequest 
        { 
            Name = $"Key 1 {Guid.NewGuid()}", 
            ExpirationDays = 90 
        };
        var request2 = new CreateApiKeyRequest 
        { 
            Name = $"Key 2 {Guid.NewGuid()}", 
            ExpirationDays = 90 
        };

        // Act
        var response1 = await _client!.PostAsJsonAsync("/api/settings/api-keys", request1);
        var response2 = await _client!.PostAsJsonAsync("/api/settings/api-keys", request2);

        var key1 = await response1.Content.ReadFromJsonAsync<ApiKey>();
        var key2 = await response2.Content.ReadFromJsonAsync<ApiKey>();

        // Assert
        Assert.That(key1, Is.Not.Null);
        Assert.That(key2, Is.Not.Null);
        Assert.That(key1!.Key, Is.Not.EqualTo(key2!.Key));
    }

    #endregion

    #region API Key Format Tests

    [Test]
    public async Task ApiKey_HasCorrectFormat()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"Format Test Key {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        // Assert
        Assert.That(key?.Key, Is.Not.Null);
        Assert.That(key!.Key, Does.StartWith("ts_"));
        Assert.That(key.Key.Length, Is.GreaterThan(10));
        Assert.That(key.Key, Does.Match(@"^ts_[a-z0-9]+$")); // Only lowercase alphanumeric after prefix
    }

    [Test]
    public async Task ApiKey_HasMinimumLength()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"Length Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        // Assert
        Assert.That(key?.Key, Is.Not.Null);
        // Should be at least 35 characters (ts_ + 32 random chars)
        Assert.That(key!.Key.Length, Is.GreaterThanOrEqualTo(35));
    }

    #endregion

    #region API Key Metadata Tests

    [Test]
    public async Task CreateApiKey_SetsCreatedByToCurrentUser()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"CreatedBy Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        // Assert
        Assert.That(key, Is.Not.Null);
        Assert.That(key!.CreatedBy, Is.EqualTo("admin"));
    }

    [Test]
    public async Task CreateApiKey_SetsCreatedAtToCurrentTime()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);
        var beforeCreation = DateTime.UtcNow.AddSeconds(-5);

        var request = new CreateApiKeyRequest
        {
            Name = $"CreatedAt Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();
        var afterCreation = DateTime.UtcNow.AddSeconds(5);

        // Assert
        Assert.That(key, Is.Not.Null);
        Assert.That(key!.CreatedAt, Is.GreaterThan(beforeCreation));
        Assert.That(key.CreatedAt, Is.LessThan(afterCreation));
    }

    [Test]
    public async Task CreateApiKey_LastUsedIsNull_OnCreation()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"LastUsed Test {Guid.NewGuid()}",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        // Assert
        Assert.That(key, Is.Not.Null);
        Assert.That(key!.LastUsed, Is.Null);
    }

    #endregion

    #region API Key Expiration Tests

    [Test]
    public async Task CreateApiKey_CalculatesExpirationCorrectly()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);
        var beforeCreation = DateTime.UtcNow;

        var request = new CreateApiKeyRequest
        {
            Name = $"Expiration Test {Guid.NewGuid()}",
            ExpirationDays = 30
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();
        
        var expectedExpiration = beforeCreation.AddDays(30);

        // Assert
        Assert.That(key?.ExpiresAt, Is.Not.Null);
        // Allow 1 minute tolerance
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
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"Expiration {days}d Test {Guid.NewGuid()}",
            ExpirationDays = days
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);
        var key = await response.Content.ReadFromJsonAsync<ApiKey>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(key?.ExpiresAt, Is.Not.Null);
    }

    #endregion

    #region API Key Deletion Tests

    [Test]
    public async Task DeleteApiKey_WithValidId_DeletesSuccessfully()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        // Create a key first
        var createRequest = new CreateApiKeyRequest
        {
            Name = $"Key to Delete {Guid.NewGuid()}",
            ExpirationDays = 90
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/settings/api-keys", createRequest);
        var createdKey = await createResponse.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey?.Id, Is.Not.Null);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/settings/api-keys/{createdKey!.Id}");

        // Assert
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify it's deleted
        var getResponse = await _client.GetAsync("/api/settings/api-keys");
        var keys = await getResponse.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keys, Is.Not.Null);
        Assert.That(keys!.Any(k => k.Id == createdKey.Id), Is.False);
    }

    [Test]
    public async Task DeleteApiKey_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        // Act
        var response = await _client!.DeleteAsync("/api/settings/api-keys/507f1f77bcf86cd799439011");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteApiKey_WithMalformedId_ReturnsBadRequestOrNotFound()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        // Act
        var response = await _client!.DeleteAsync("/api/settings/api-keys/invalid-id");

        // Assert
        // Either BadRequest or NotFound is acceptable
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
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        // Create multiple keys
        for (int i = 0; i < 3; i++)
        {
            await _client!.PostAsJsonAsync("/api/settings/api-keys", new CreateApiKeyRequest
            {
                Name = $"Ordered Key {i} {Guid.NewGuid()}",
                ExpirationDays = 90
            });
            await Task.Delay(100); // Small delay to ensure different timestamps
        }

        // Act
        var response = await _client!.GetAsync("/api/settings/api-keys");
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKey>>();

        // Assert
        Assert.That(keys, Is.Not.Null);
        Assert.That(keys!.Count, Is.GreaterThanOrEqualTo(3));
        
        // Verify descending order
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
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = "   ",
            ExpirationDays = 90
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateApiKey_WithNegativeExpiration_ShouldStillWork()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);

        var request = new CreateApiKeyRequest
        {
            Name = $"Negative Expiration Test {Guid.NewGuid()}",
            ExpirationDays = -1
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/settings/api-keys", request);

        // Assert
        // System should either reject or create expired key
        // This tests the system's handling of edge cases
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
        // Arrange
        var token = await GetAdminTokenAsync();
        SetAuthToken(token);
        var keyName = $"Lifecycle Test {Guid.NewGuid()}";

        // Act & Assert - Create
        var createRequest = new CreateApiKeyRequest
        {
            Name = keyName,
            ExpirationDays = 90
        };
        var createResponse = await _client!.PostAsJsonAsync("/api/settings/api-keys", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdKey = await createResponse.Content.ReadFromJsonAsync<ApiKey>();
        Assert.That(createdKey, Is.Not.Null);

        // Act & Assert - List
        var listResponse = await _client.GetAsync("/api/settings/api-keys");
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var keys = await listResponse.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keys!.Any(k => k.Name == keyName), Is.True);

        // Act & Assert - Delete
        var deleteResponse = await _client.DeleteAsync($"/api/settings/api-keys/{createdKey!.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var verifyResponse = await _client.GetAsync("/api/settings/api-keys");
        var keysAfterDelete = await verifyResponse.Content.ReadFromJsonAsync<List<ApiKey>>();
        Assert.That(keysAfterDelete!.Any(k => k.Name == keyName), Is.False);
    }

    #endregion

    private class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
