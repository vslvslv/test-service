using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TestService.Api.Models;

namespace TestService.Tests;

[TestFixture]
public class AgentApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetAll_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/agents");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Create_WithValidAgent_ReturnsCreated()
    {
        // Arrange
        var agent = new Agent
        {
            Username = "testuser",
            Password = "Test@123",
            UserId = "user001",
            FirstName = "John",
            LastName = "Doe",
            BrandId = "brand123",
            LabelId = "label456",
            OrientationType = "vertical",
            AgentType = "support"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/agents", agent);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var createdAgent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.That(createdAgent, Is.Not.Null);
        Assert.That(createdAgent!.Username, Is.EqualTo(agent.Username));
        Assert.That(createdAgent.FirstName, Is.EqualTo(agent.FirstName));
        Assert.That(createdAgent.LastName, Is.EqualTo(agent.LastName));
        Assert.That(createdAgent.BrandId, Is.EqualTo(agent.BrandId));
        Assert.That(createdAgent.Id, Is.Not.Null);
    }

    [Test]
    public async Task GetById_WithExistingId_ReturnsAgent()
    {
        // Arrange - Create an agent first
        var agent = new Agent
        {
            Username = "getbyidtest",
            Password = "Test@123",
            UserId = "user002",
            FirstName = "Jane",
            LastName = "Smith",
            BrandId = "brand999",
            LabelId = "label999",
            OrientationType = "horizontal",
            AgentType = "sales"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agents", agent);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        // Act
        var response = await _client.GetAsync($"/api/agents/{createdAgent!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrievedAgent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.That(retrievedAgent, Is.Not.Null);
        Assert.That(retrievedAgent!.Id, Is.EqualTo(createdAgent.Id));
        Assert.That(retrievedAgent.Username, Is.EqualTo(agent.Username));
    }

    [Test]
    public async Task GetById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = "507f1f77bcf86cd799439011";

        // Act
        var response = await _client.GetAsync($"/api/agents/{nonExistingId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetByUsername_WithExistingUsername_ReturnsAgent()
    {
        // Arrange - Create an agent first
        var username = $"uniqueuser_{Guid.NewGuid()}";
        var agent = new Agent
        {
            Username = username,
            Password = "Test@123",
            UserId = "user003",
            FirstName = "Bob",
            LastName = "Johnson",
            BrandId = "brand123",
            LabelId = "label123",
            OrientationType = "vertical",
            AgentType = "technical"
        };
        await _client.PostAsJsonAsync("/api/agents", agent);

        // Act
        var response = await _client.GetAsync($"/api/agents/username/{username}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrievedAgent = await response.Content.ReadFromJsonAsync<Agent>();
        Assert.That(retrievedAgent, Is.Not.Null);
        Assert.That(retrievedAgent!.Username, Is.EqualTo(username));
    }

    [Test]
    public async Task GetByBrandId_ReturnsFilteredAgents()
    {
        // Arrange
        var brandId = $"brand_{Guid.NewGuid()}";
        var agent1 = new Agent
        {
            Username = $"user1_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user004",
            FirstName = "Alice",
            LastName = "Brown",
            BrandId = brandId,
            LabelId = "label001",
            OrientationType = "vertical",
            AgentType = "support"
        };
        var agent2 = new Agent
        {
            Username = $"user2_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user005",
            FirstName = "Charlie",
            LastName = "Davis",
            BrandId = brandId,
            LabelId = "label002",
            OrientationType = "horizontal",
            AgentType = "sales"
        };

        await _client.PostAsJsonAsync("/api/agents", agent1);
        await _client.PostAsJsonAsync("/api/agents", agent2);

        // Act
        var response = await _client.GetAsync($"/api/agents/brand/{brandId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var agents = await response.Content.ReadFromJsonAsync<List<Agent>>();
        Assert.That(agents, Is.Not.Null);
        Assert.That(agents!.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(agents.All(a => a.BrandId == brandId), Is.True);
    }

    [Test]
    public async Task GetByLabelId_ReturnsFilteredAgents()
    {
        // Arrange
        var labelId = $"label_{Guid.NewGuid()}";
        var agent = new Agent
        {
            Username = $"labeluser_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user006",
            FirstName = "Emma",
            LastName = "Wilson",
            BrandId = "brand001",
            LabelId = labelId,
            OrientationType = "vertical",
            AgentType = "support"
        };

        await _client.PostAsJsonAsync("/api/agents", agent);

        // Act
        var response = await _client.GetAsync($"/api/agents/label/{labelId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var agents = await response.Content.ReadFromJsonAsync<List<Agent>>();
        Assert.That(agents, Is.Not.Null);
        Assert.That(agents!.Any(a => a.LabelId == labelId), Is.True);
    }

    [Test]
    public async Task GetByOrientationType_ReturnsFilteredAgents()
    {
        // Arrange
        var orientationType = "vertical";
        var agent = new Agent
        {
            Username = $"orientuser_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user007",
            FirstName = "Frank",
            LastName = "Miller",
            BrandId = "brand001",
            LabelId = "label001",
            OrientationType = orientationType,
            AgentType = "support"
        };

        await _client.PostAsJsonAsync("/api/agents", agent);

        // Act
        var response = await _client.GetAsync($"/api/agents/orientation/{orientationType}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var agents = await response.Content.ReadFromJsonAsync<List<Agent>>();
        Assert.That(agents, Is.Not.Null);
        Assert.That(agents!.Any(a => a.OrientationType == orientationType), Is.True);
    }

    [Test]
    public async Task GetByAgentType_ReturnsFilteredAgents()
    {
        // Arrange
        var agentType = "technical";
        var agent = new Agent
        {
            Username = $"typeuser_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user008",
            FirstName = "Grace",
            LastName = "Taylor",
            BrandId = "brand001",
            LabelId = "label001",
            OrientationType = "horizontal",
            AgentType = agentType
        };

        await _client.PostAsJsonAsync("/api/agents", agent);

        // Act
        var response = await _client.GetAsync($"/api/agents/type/{agentType}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var agents = await response.Content.ReadFromJsonAsync<List<Agent>>();
        Assert.That(agents, Is.Not.Null);
        Assert.That(agents!.Any(a => a.AgentType == agentType), Is.True);
    }

    [Test]
    public async Task Update_WithValidData_ReturnsNoContent()
    {
        // Arrange - Create an agent first
        var agent = new Agent
        {
            Username = $"updateuser_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user009",
            FirstName = "Henry",
            LastName = "Anderson",
            BrandId = "brand001",
            LabelId = "label001",
            OrientationType = "vertical",
            AgentType = "support"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agents", agent);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        // Modify the agent
        createdAgent!.FirstName = "Henry Updated";
        createdAgent.AgentType = "technical";

        // Act
        var response = await _client.PutAsJsonAsync($"/api/agents/{createdAgent.Id}", createdAgent);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify the update
        var getResponse = await _client.GetAsync($"/api/agents/{createdAgent.Id}");
        var updatedAgent = await getResponse.Content.ReadFromJsonAsync<Agent>();
        Assert.That(updatedAgent!.FirstName, Is.EqualTo("Henry Updated"));
        Assert.That(updatedAgent.AgentType, Is.EqualTo("technical"));
    }

    [Test]
    public async Task Delete_WithExistingId_ReturnsNoContent()
    {
        // Arrange - Create an agent first
        var agent = new Agent
        {
            Username = $"deleteuser_{Guid.NewGuid()}",
            Password = "Test@123",
            UserId = "user010",
            FirstName = "Ivy",
            LastName = "Thomas",
            BrandId = "brand001",
            LabelId = "label001",
            OrientationType = "vertical",
            AgentType = "support"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/agents", agent);
        var createdAgent = await createResponse.Content.ReadFromJsonAsync<Agent>();

        // Act
        var response = await _client.DeleteAsync($"/api/agents/{createdAgent!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/agents/{createdAgent.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
