using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

/// <summary>
/// End-to-end CRUD coverage for dynamic entities against a single shared schema
/// created in OneTimeSetUp. Tests are independent and may run in any order.
/// </summary>
[TestFixture]
public class DynamicEntityTests : IntegrationTestBase
{
    private string _entityType = string.Empty;

    protected override async Task OnOneTimeSetUp()
    {
        _entityType = CreateUniqueName("Agent");

        var schema = new EntitySchema
        {
            EntityName = _entityType,
            Fields = new List<FieldDefinition>
            {
                new() { Name = "username", Type = "string", Required = true, IsUnique = false },
                new() { Name = "password", Type = "string", Required = true, IsUnique = false },
                new() { Name = "userId", Type = "string", Required = true, IsUnique = false },
                new() { Name = "firstName", Type = "string", Required = false, IsUnique = false },
                new() { Name = "lastName", Type = "string", Required = false, IsUnique = false },
                new() { Name = "brandId", Type = "string", Required = false, IsUnique = false },
                new() { Name = "labelId", Type = "string", Required = false, IsUnique = false },
                new() { Name = "orientationType", Type = "string", Required = false, IsUnique = false },
                new() { Name = "agentType", Type = "string", Required = false, IsUnique = false }
            },
            FilterableFields = new List<string>
            {
                "username", "brandId", "labelId", "orientationType", "agentType"
            }
        };

        var response = await Client.PostAsJsonAsync("/api/schemas", schema);
        // Conflict is acceptable here: a previous run may have left the fixture schema behind.
        if (response.StatusCode == HttpStatusCode.Conflict) return;
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Schema setup must return Created (or Conflict if already present), got {response.StatusCode}");
    }

    protected override async Task OnOneTimeTearDown()
    {
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, _entityType);
    }

    [Test]
    public async Task GetAllSchemas_IncludesFixtureSchema()
    {
        var response = await Client.GetAsync("/api/schemas");

        AssertStatusCode(response, HttpStatusCode.OK);
        var schemas = await response.Content.ReadFromJsonAsync<List<EntitySchema>>();
        Assert.That(schemas, Is.Not.Null);
        Assert.That(schemas!.Any(s => s.EntityName == _entityType), Is.True);
    }

    [Test]
    public async Task GetSchemaByName_ReturnsSchema()
    {
        var response = await Client.GetAsync($"/api/schemas/{_entityType}");

        AssertStatusCode(response, HttpStatusCode.OK);
        var schema = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema!.EntityName, Is.EqualTo(_entityType));
        Assert.That(schema.Fields.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateEntity_WithValidData_ReturnsCreated()
    {
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", $"john.doe_{Guid.NewGuid()}" },
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

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null);
        Assert.That(created.EntityType, Is.EqualTo(_entityType));
        Assert.That(GetFieldString(created, "firstName"), Is.EqualTo("John"));
    }

    [Test]
    public async Task GetAllEntities_AfterCreate_ReturnsAtLeastOne()
    {
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "username", $"listuser_{Guid.NewGuid()}" },
                { "password", "Test@123" },
                { "userId", "user_list" }
            }
        };
        var createResponse = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);
        AssertStatusCode(createResponse, HttpStatusCode.Created);

        var response = await Client.GetAsync($"/api/entities/{_entityType}");

        AssertStatusCode(response, HttpStatusCode.OK);
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetEntityById_ReturnsEntity()
    {
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

        var createResponse = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);
        AssertStatusCode(createResponse, HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);

        var response = await Client.GetAsync($"/api/entities/{_entityType}/{created!.Id}");

        AssertStatusCode(response, HttpStatusCode.OK);
        var retrieved = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task FilterEntities_ByBrandId_ReturnsFilteredResults()
    {
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
            await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);
        }

        var response = await Client.GetAsync($"/api/entities/{_entityType}/filter/brandId/{brandId}");

        AssertStatusCode(response, HttpStatusCode.OK);
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.EqualTo(2));
        Assert.That(entities.All(e => e.Fields["brandId"]?.ToString() == brandId), Is.True);
    }

    [Test]
    public async Task FilterEntities_ByUsername_ReturnsEntity()
    {
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
        await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        var response = await Client.GetAsync($"/api/entities/{_entityType}/filter/username/{username}");

        AssertStatusCode(response, HttpStatusCode.OK);
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Any(e => e.Fields["username"]?.ToString() == username), Is.True);
    }

    [Test]
    public async Task UpdateEntity_WithValidData_ReturnsNoContent()
    {
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
        var createResponse = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);
        AssertStatusCode(createResponse, HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);

        created!.Fields["firstName"] = "Updated";
        created.Fields["agentType"] = "technical";

        var response = await Client.PutAsJsonAsync($"/api/entities/{_entityType}/{created.Id}", created);

        AssertStatusCode(response, HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/entities/{_entityType}/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(GetFieldString(updated!, "firstName"), Is.EqualTo("Updated"));
        Assert.That(GetFieldString(updated, "agentType"), Is.EqualTo("technical"));
    }

    [Test]
    public async Task DeleteEntity_WithValidId_ReturnsNoContent()
    {
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
        var createResponse = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);
        AssertStatusCode(createResponse, HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);

        var response = await Client.DeleteAsync($"/api/entities/{_entityType}/{created!.Id}");

        AssertStatusCode(response, HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/entities/{_entityType}/{created.Id}");
        AssertStatusCode(getResponse, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateEntity_WithMissingRequiredField_ReturnsBadRequest()
    {
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "password", "Test@123" },
                { "userId", "user006" }
            }
        };

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateEntity_ForNonExistentSchema_ReturnsNotFound()
    {
        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "field1", "value1" }
            }
        };

        var response = await Client.PostAsJsonAsync("/api/entities/NonExistentType", entity);

        AssertStatusCode(response, HttpStatusCode.NotFound);
    }
}
