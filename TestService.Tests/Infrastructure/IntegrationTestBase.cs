using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using TestService.Api.Models;
using TestService.Tests;

namespace TestService.Tests.Infrastructure;

/// <summary>
/// Base class for all integration tests with common setup and utilities
/// </summary>
public abstract class IntegrationTestBase
{
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "Admin@123";

    protected HttpClient Client { get; private set; } = null!;
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Point the in-process API at the Testcontainers Mongo instance so the
                // suite is hermetic and does not require a developer-managed Mongo.
                // Added after default config so it overrides appsettings.json.
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["MongoDbSettings:ConnectionString"] = MongoDbContainerFixture.ConnectionString,
                        ["MongoDbSettings:DatabaseName"] = "TestServiceDb"
                    });
                });
            });
        Client = Factory.CreateClient();

        if (AuthenticateAsAdminByDefault)
        {
            await SetAdminAuthTokenAsync();
        }

        await OnOneTimeSetUp();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await OnOneTimeTearDown();
        Client?.Dispose();
        Factory?.Dispose();
    }

    [SetUp]
    public async Task SetUp()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");
        Client.DefaultRequestHeaders.Authorization = null;

        if (AuthenticateAsAdminByDefault)
        {
            await SetAdminAuthTokenAsync();
        }

        await OnSetUp();
    }

    [TearDown]
    public async Task TearDown()
    {
        await OnTearDown();
        Client.DefaultRequestHeaders.Remove("Authorization");
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Override this method to perform additional one-time setup
    /// </summary>
    protected virtual Task OnOneTimeSetUp() => Task.CompletedTask;

    /// <summary>
    /// Override this method to perform additional one-time teardown
    /// </summary>
    protected virtual Task OnOneTimeTearDown() => Task.CompletedTask;

    /// <summary>
    /// Override this method to perform setup before each test
    /// </summary>
    protected virtual Task OnSetUp() => Task.CompletedTask;

    /// <summary>
    /// Override this method to perform teardown after each test
    /// </summary>
    protected virtual Task OnTearDown() => Task.CompletedTask;

    /// <summary>
    /// Integration tests default to admin authentication unless explicitly disabled.
    /// </summary>
    protected virtual bool AuthenticateAsAdminByDefault => true;

    /// <summary>
    /// Asserts that the response status code matches the expected value
    /// </summary>
    protected void AssertStatusCode(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expected),
            $"Expected status code {expected} but got {response.StatusCode}. Response: {response.Content.ReadAsStringAsync().Result}");
    }

    /// <summary>
    /// Asserts that the response is successful (2xx status code)
    /// </summary>
    protected void AssertSuccess(HttpResponseMessage response)
    {
        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Expected successful response but got {response.StatusCode}. Response: {response.Content.ReadAsStringAsync().Result}");
    }

    /// <summary>
    /// Creates a unique test identifier for isolation
    /// </summary>
    protected string CreateUniqueId() => Guid.NewGuid().ToString();

    /// <summary>
    /// Creates a unique test name
    /// </summary>
    protected string CreateUniqueName(string prefix = "test") => $"{prefix}_{Guid.NewGuid()}";

    /// <summary>
    /// Extracts a field value from DynamicEntity, handling JsonElement properly
    /// </summary>
    protected string? GetFieldString(DynamicEntity entity, string fieldName)
    {
        return entity.GetFieldString(fieldName);
    }

    /// <summary>
    /// Extracts a field value from DynamicEntity with type conversion
    /// </summary>
    protected T? GetFieldValue<T>(DynamicEntity entity, string fieldName)
    {
        return entity.GetFieldValue<T>(fieldName);
    }

    protected async Task SetAdminAuthTokenAsync()
    {
        var token = await GetAdminTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected async Task<string> GetAdminTokenAsync()
    {
        HttpResponseMessage? response = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = DefaultAdminUsername,
                Password = DefaultAdminPassword
            });

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                break;
            }

            await Task.Delay(250);
        }

        if (response == null)
        {
            throw new InvalidOperationException("Unable to execute login request for admin user.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Admin login failed with 401 after 5 retries. " +
                $"The default admin user did not finish seeding within the retry window, " +
                $"or the API failed to connect to the test MongoDB container. " +
                $"Verify Docker is running (the suite uses Testcontainers.MongoDb). " +
                $"Response body: {body}");
        }

        AssertStatusCode(response, HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        Assert.That(login!.Token, Is.Not.Empty);
        return login.Token;
    }
}
