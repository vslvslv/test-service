using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Environments;

/// <summary>
/// Tests for environment CRUD operations
/// </summary>
[TestFixture]
public class EnvironmentCrudTests : IntegrationTestBase
{
    private string? _adminToken;

    protected override async void OnOneTimeSetUp()
    {
        // Login as admin to get token
        var loginRequest = new { username = "admin", password = "Admin@123" };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _adminToken = loginResult!["token"].ToString();
    }

    [Test]
    public async Task GetAllEnvironments_ReturnsDefaultEnvironments()
    {
        // Act
        var response = await Client.GetAsync("/api/environments");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var environments = await response.Content.ReadFromJsonAsync<List<EnvironmentResponse>>();
        Assert.That(environments, Is.Not.Null);
        Assert.That(environments!.Count, Is.GreaterThanOrEqualTo(3));
        
        // Check for default environments
        Assert.That(environments.Any(e => e.Name == "dev"), Is.True);
        Assert.That(environments.Any(e => e.Name == "staging"), Is.True);
        Assert.That(environments.Any(e => e.Name == "production"), Is.True);
    }

    [Test]
    public async Task GetAllEnvironments_WithStatistics_IncludesStats()
    {
        // Act
        var response = await Client.GetAsync("/api/environments?includeStatistics=true");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var environments = await response.Content.ReadFromJsonAsync<List<EnvironmentResponse>>();
        Assert.That(environments, Is.Not.Null);
        Assert.That(environments!.All(e => e.Statistics != null), Is.True);
    }

    [Test]
    public async Task GetEnvironmentById_WithValidId_ReturnsEnvironment()
    {
        // Arrange - Get an environment first
        var allResponse = await Client.GetAsync("/api/environments");
        var environments = await allResponse.Content.ReadFromJsonAsync<List<EnvironmentResponse>>();
        var devEnv = environments!.First(e => e.Name == "dev");

        // Act
        var response = await Client.GetAsync($"/api/environments/{devEnv.Id}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var environment = await response.Content.ReadFromJsonAsync<EnvironmentResponse>();
        Assert.That(environment, Is.Not.Null);
        Assert.That(environment!.Name, Is.EqualTo("dev"));
    }

    [Test]
    public async Task GetEnvironmentById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/environments/507f1f77bcf86cd799439011");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetEnvironmentByName_WithValidName_ReturnsEnvironment()
    {
        // Act
        var response = await Client.GetAsync("/api/environments/name/dev");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var environment = await response.Content.ReadFromJsonAsync<EnvironmentResponse>();
        Assert.That(environment, Is.Not.Null);
        Assert.That(environment!.Name, Is.EqualTo("dev"));
        Assert.That(environment.DisplayName, Is.EqualTo("Development"));
    }

    [Test]
    public async Task GetEnvironmentByName_WithInvalidName_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/environments/name/nonexistent");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateEnvironment_WithValidData_ReturnsCreated()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        var newEnvironment = new
        {
            name = $"qa-{Guid.NewGuid()}",
            displayName = "QA Environment",
            description = "Quality Assurance testing",
            url = "https://qa.example.com",
            color = "#0000ff",
            order = 4,
            configuration = new Dictionary<string, string>
            {
                { "apiKey", "qa-key" },
                { "timeout", "30" }
            },
            tags = new[] { "qa", "testing" }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments", newEnvironment);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<EnvironmentResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Name, Is.EqualTo(newEnvironment.name));
        Assert.That(created.DisplayName, Is.EqualTo(newEnvironment.displayName));
        Assert.That(created.Configuration.Count, Is.EqualTo(2));
        Assert.That(created.Tags.Count, Is.EqualTo(2));

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task CreateEnvironment_WithUppercaseName_ReturnsBadRequest()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        var newEnvironment = new
        {
            name = "QA-ENV",
            displayName = "QA Environment"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments", newEnvironment);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task CreateEnvironment_WithDuplicateName_ReturnsConflict()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        var newEnvironment = new
        {
            name = "dev",
            displayName = "Duplicate Dev"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments", newEnvironment);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task CreateEnvironment_WithoutAdminRole_ReturnsUnauthorized()
    {
        // Arrange - Create without token
        var newEnvironment = new
        {
            name = "test",
            displayName = "Test"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments", newEnvironment);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UpdateEnvironment_WithValidData_ReturnsNoContent()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        // Create environment first
        var createRequest = new
        {
            name = $"update-test-{Guid.NewGuid()}",
            displayName = "Original Name"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/environments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();

        // Update request
        var updateRequest = new
        {
            displayName = "Updated Name",
            description = "Updated description",
            isActive = true
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{created!.Id}", updateRequest);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify
        var getResponse = await Client.GetAsync($"/api/environments/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();
        Assert.That(updated!.DisplayName, Is.EqualTo("Updated Name"));
        Assert.That(updated.Description, Is.EqualTo("Updated description"));

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task DeleteEnvironment_WithNoEntities_ReturnsNoContent()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        // Create environment
        var createRequest = new
        {
            name = $"delete-test-{Guid.NewGuid()}",
            displayName = "To Delete"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/environments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();

        // Act
        var response = await Client.DeleteAsync($"/api/environments/{created!.Id}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify
        var getResponse = await Client.GetAsync($"/api/environments/{created.Id}");
        AssertStatusCode(getResponse, HttpStatusCode.NotFound);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task ActivateEnvironment_ChangesStatus_ReturnsNoContent()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        // Create and deactivate environment
        var createRequest = new
        {
            name = $"activate-test-{Guid.NewGuid()}",
            displayName = "To Activate"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/environments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();

        // Deactivate first
        await Client.PostAsync($"/api/environments/{created!.Id}/deactivate", null);

        // Act
        var response = await Client.PostAsync($"/api/environments/{created.Id}/activate", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify
        var getResponse = await Client.GetAsync($"/api/environments/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();
        Assert.That(updated!.IsActive, Is.True);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task DeactivateEnvironment_ChangesStatus_ReturnsNoContent()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        // Create environment
        var createRequest = new
        {
            name = $"deactivate-test-{Guid.NewGuid()}",
            displayName = "To Deactivate"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/environments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();

        // Act
        var response = await Client.PostAsync($"/api/environments/{created!.Id}/deactivate", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify
        var getResponse = await Client.GetAsync($"/api/environments/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();
        Assert.That(updated!.IsActive, Is.False);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task GetEnvironmentStatistics_ReturnsCorrectStats()
    {
        // Act
        var response = await Client.GetAsync("/api/environments/dev/statistics");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var stats = await response.Content.ReadFromJsonAsync<EnvironmentStatistics>();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats!.TotalEntities, Is.GreaterThanOrEqualTo(0));
        Assert.That(stats.AvailableEntities, Is.GreaterThanOrEqualTo(0));
        Assert.That(stats.ConsumedEntities, Is.GreaterThanOrEqualTo(0));
    }
}

/// <summary>
/// Tests for environment validation and edge cases
/// </summary>
[TestFixture]
public class EnvironmentValidationTests : IntegrationTestBase
{
    private string? _adminToken;

    protected override async void OnOneTimeSetUp()
    {
        // Login as admin
        var loginRequest = new { username = "admin", password = "Admin@123" };
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        _adminToken = loginResult!["token"].ToString();
    }

    [Test]
    public async Task CreateEnvironment_WithSpecialCharactersInName_ReturnsBadRequest()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        var newEnvironment = new
        {
            name = "qa_env!@#",
            displayName = "QA"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments", newEnvironment);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task CreateEnvironment_WithHyphensInName_Succeeds()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        var newEnvironment = new
        {
            name = $"qa-env-{Guid.NewGuid()}",
            displayName = "QA Environment"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/environments", newEnvironment);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task GetAllEnvironments_WithInactiveFilter_ReturnsOnlyActive()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        // Create and deactivate an environment
        var createRequest = new
        {
            name = $"inactive-{Guid.NewGuid()}",
            displayName = "Inactive"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/environments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();
        await Client.PostAsync($"/api/environments/{created!.Id}/deactivate", null);

        // Act - Get without includeInactive
        var response = await Client.GetAsync("/api/environments?includeInactive=false");

        // Assert
        var environments = await response.Content.ReadFromJsonAsync<List<EnvironmentResponse>>();
        Assert.That(environments!.All(e => e.IsActive), Is.True);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    [Test]
    public async Task GetAllEnvironments_IncludeInactive_ReturnsAll()
    {
        // Arrange
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_adminToken}");
        
        // Create and deactivate an environment
        var createRequest = new
        {
            name = $"inactive-all-{Guid.NewGuid()}",
            displayName = "Inactive All"
        };
        var createResponse = await Client.PostAsJsonAsync("/api/environments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EnvironmentResponse>();
        await Client.PostAsync($"/api/environments/{created!.Id}/deactivate", null);

        // Act
        var response = await Client.GetAsync("/api/environments?includeInactive=true");

        // Assert
        var environments = await response.Content.ReadFromJsonAsync<List<EnvironmentResponse>>();
        Assert.That(environments!.Any(e => !e.IsActive), Is.True);

        // Cleanup
        Client.DefaultRequestHeaders.Remove("Authorization");
    }
}
