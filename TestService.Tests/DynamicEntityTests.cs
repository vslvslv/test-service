using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TestService.Api.Models;

namespace TestService.Tests;

[TestFixture]
public class DynamicEntityTests
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

    [Test, Order(1)]
    public async Task CreateSchema_ForAgentEntity_ReturnsCreated()
    {
        // Arrange
        var schema = new EntitySchema
        {
            EntityName = "Agent",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "username", Type = "string", Required = true },
                new() { Name = "password", Type = "string", Required = true },
                new() { Name = "userId", Type = "string", Required = true },
                new() { Name = "firstName", Type = "string", Required = false },
                new() { Name = "lastName", Type = "string", Required = false },
                new() { Name = "brandId", Type = "string", Required = false },
                new() { Name = "labelId", Type = "string", Required = false },
                new() { Name = "orientationType", Type = "string", Required = false },
                new() { Name = "agentType", Type = "string", Required = false }
            },
            FilterableFields = new List<string> 
            { 
                "username", "brandId", "labelId", "orientationType", "agentType" 
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created).Or.EqualTo(HttpStatusCode.Conflict));
    }

    [Test, Order(2)]
    public async Task GetAllSchemas_ReturnsSchemas()
    {
        // Act
        var response = await _client.GetAsync("/api/schemas");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var schemas = await response.Content.ReadFromJsonAsync<List<EntitySchema>>();
        Assert.That(schemas, Is.Not.Null);
        Assert.That(schemas!.Any(s => s.EntityName == "Agent"), Is.True);
    }

    [Test, Order(3)]
    public async Task GetSchemaByName_ReturnsSchema()
    {
        // Act
        var response = await _client.GetAsync("/api/schemas/Agent");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var schema = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema!.EntityName, Is.EqualTo("Agent"));
        Assert.That(schema.Fields.Count, Is.GreaterThan(0));
    }

    [Test, Order(4)]
    public async Task CreateEntity_WithValidData_ReturnsCreated()
    {
        // Arrange
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", "john.doe" },
                { "password", "SecurePass@123" },
                { "userId", "user001" },
                { "firstName", "John" },
                { "lastName", "Doe" },
                { "brandId", "brand123" },
                { "labelId", "label456" },
                { "orientationType", "vertical" },
                { "agentType", "support" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/entities/Agent", entity);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null);
        Assert.That(created.EntityType, Is.EqualTo("Agent"));
        Assert.That(created.Fields["username"], Is.EqualTo("john.doe"));
    }

    [Test, Order(5)]
    public async Task GetAllEntities_ReturnsEntities()
    {
        // Act
        var response = await _client.GetAsync("/api/entities/Agent");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThan(0));
    }

    [Test, Order(6)]
    public async Task GetEntityById_ReturnsEntity()
    {
        // Arrange - Create an entity first
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", $"testuser_{Guid.NewGuid()}" },
                { "password", "Test@123" },
                { "userId", "user002" },
                { "firstName", "Test" },
                { "lastName", "User" },
                { "brandId", "brand999" },
                { "labelId", "label999" },
                { "orientationType", "horizontal" },
                { "agentType", "sales" }
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/entities/Agent", entity);
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();

        // Act
        var response = await _client.GetAsync($"/api/entities/Agent/{created!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrieved = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(created.Id));
    }

    [Test, Order(7)]
    public async Task FilterEntities_ByBrandId_ReturnsFilteredResults()
    {
        // Arrange - Create entities with specific brandId
        var brandId = $"brand_{Guid.NewGuid()}";
        for (int i = 0; i < 2; i++)
        {
            var entity = new DynamicEntity
            {
                Fields = new Dictionary<string, object?>
                {
                    { "username", $"user_{Guid.NewGuid()}" },
                    { "password", "Test@123" },
                    { "userId", $"user00{i}" },
                    { "firstName", $"User{i}" },
                    { "lastName", "Test" },
                    { "brandId", brandId },
                    { "labelId", "label001" },
                    { "orientationType", "vertical" },
                    { "agentType", "support" }
                }
            };
            await _client.PostAsJsonAsync("/api/entities/Agent", entity);
        }

        // Act
        var response = await _client.GetAsync($"/api/entities/Agent/filter/brandId/{brandId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(entities.All(e => e.Fields["brandId"]?.ToString() == brandId), Is.True);
    }

    [Test, Order(8)]
    public async Task FilterEntities_ByUsername_ReturnsEntity()
    {
        // Arrange - Create entity with unique username
        var username = $"unique_{Guid.NewGuid()}";
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", username },
                { "password", "Test@123" },
                { "userId", "user003" },
                { "firstName", "Unique" },
                { "lastName", "User" },
                { "brandId", "brand001" },
                { "labelId", "label001" },
                { "orientationType", "vertical" },
                { "agentType", "technical" }
            }
        };
        await _client.PostAsJsonAsync("/api/entities/Agent", entity);

        // Act
        var response = await _client.GetAsync($"/api/entities/Agent/filter/username/{username}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Any(e => e.Fields["username"]?.ToString() == username), Is.True);
    }

    [Test, Order(9)]
    public async Task UpdateEntity_WithValidData_ReturnsNoContent()
    {
        // Arrange - Create an entity
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", $"updateuser_{Guid.NewGuid()}" },
                { "password", "Test@123" },
                { "userId", "user004" },
                { "firstName", "Original" },
                { "lastName", "Name" },
                { "brandId", "brand001" },
                { "labelId", "label001" },
                { "orientationType", "vertical" },
                { "agentType", "support" }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/entities/Agent", entity);
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();

        // Modify
        created!.Fields["firstName"] = "Updated";
        created.Fields["agentType"] = "technical";

        // Act
        var response = await _client.PutAsJsonAsync($"/api/entities/Agent/{created.Id}", created);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify update
        var getResponse = await _client.GetAsync($"/api/entities/Agent/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(updated!.Fields["firstName"], Is.EqualTo("Updated"));
        Assert.That(updated.Fields["agentType"], Is.EqualTo("technical"));
    }

    [Test, Order(10)]
    public async Task DeleteEntity_WithValidId_ReturnsNoContent()
    {
        // Arrange - Create an entity
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", $"deleteuser_{Guid.NewGuid()}" },
                { "password", "Test@123" },
                { "userId", "user005" },
                { "firstName", "Delete" },
                { "lastName", "Me" },
                { "brandId", "brand001" },
                { "labelId", "label001" },
                { "orientationType", "vertical" },
                { "agentType", "support" }
            }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/entities/Agent", entity);
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();

        // Act
        var response = await _client.DeleteAsync($"/api/entities/Agent/{created!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/entities/Agent/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test, Order(11)]
    public async Task CreateEntity_WithMissingRequiredField_ReturnsBadRequest()
    {
        // Arrange - Missing required field 'username'
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "password", "Test@123" },
                { "userId", "user006" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/entities/Agent", entity);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test, Order(12)]
    public async Task CreateEntity_ForNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "field1", "value1" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/entities/NonExistentType", entity);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
