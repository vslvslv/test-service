using System.Net;
using TestService.Api.Models;

namespace TestService.Tests.Infrastructure;

/// <summary>
/// Base class for all integration tests with common setup and utilities
/// </summary>
public abstract class IntegrationTestBase
{
    protected HttpClient Client { get; private set; } = null!;
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Factory = new WebApplicationFactory<Program>();
        Client = Factory.CreateClient();
        OnOneTimeSetUp();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        OnOneTimeTearDown();
        Client?.Dispose();
        Factory?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        OnSetUp();
    }

    [TearDown]
    public void TearDown()
    {
        OnTearDown();
    }

    /// <summary>
    /// Override this method to perform additional one-time setup
    /// </summary>
    protected virtual void OnOneTimeSetUp() { }

    /// <summary>
    /// Override this method to perform additional one-time teardown
    /// </summary>
    protected virtual void OnOneTimeTearDown() { }

    /// <summary>
    /// Override this method to perform setup before each test
    /// </summary>
    protected virtual void OnSetUp() { }

    /// <summary>
    /// Override this method to perform teardown after each test
    /// </summary>
    protected virtual void OnTearDown() { }

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
}
