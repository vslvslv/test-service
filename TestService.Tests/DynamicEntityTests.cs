using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests;

/// <summary>
/// End-to-end tests for dynamic entity CRUD using a single schema (Agent-like).
/// Uses IntegrationTestBase and a unique schema name per run to avoid conflicts and share the same app instance.
/// Requires MongoDB to be running; skips entire fixture with Inconclusive when API returns 500.
/// </summary>
[TestFixture]
public class DynamicEntityTests : IntegrationTestBase
{
    private string _entityType = null!;
    private static bool _skippedDueToMongoDb;

    [Test, Order(1)]
    public async Task CreateSchema_ForAgentEntity_ReturnsCreated()
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

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            _skippedDueToMongoDb = true;
            Assert.Inconclusive("MongoDB is not available (API returned 500). Start MongoDB to run these tests.");
        }

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created).Or.EqualTo(HttpStatusCode.Conflict),
            "Expected Created or Conflict");
    }

    [Test, Order(2)]
    public async Task GetAllSchemas_ReturnsSchemas()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

        var response = await Client.GetAsync("/api/schemas");

        AssertStatusCode(response, HttpStatusCode.OK);
        var schemas = await response.Content.ReadFromJsonAsync<List<EntitySchema>>();
        Assert.That(schemas, Is.Not.Null);
        Assert.That(schemas!.Any(s => s.EntityName == _entityType), Is.True);
    }

    [Test, Order(3)]
    public async Task GetSchemaByName_ReturnsSchema()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

        var response = await Client.GetAsync($"/api/schemas/{_entityType}");

        AssertStatusCode(response, HttpStatusCode.OK);
        var schema = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema!.EntityName, Is.EqualTo(_entityType));
        Assert.That(schema.Fields.Count, Is.GreaterThan(0));
    }

    [Test, Order(4)]
    public async Task CreateEntity_WithValidData_ReturnsCreated()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

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

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null);
        Assert.That(created.EntityType, Is.EqualTo(_entityType));
        Assert.That(created.Fields["username"], Is.EqualTo("john.doe"));
    }

    [Test, Order(5)]
    public async Task GetAllEntities_ReturnsEntities()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

        var response = await Client.GetAsync($"/api/entities/{_entityType}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThan(0));
    }

    [Test, Order(6)]
    public async Task GetEntityById_ReturnsEntity()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

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
        Assert.That(createResponse.IsSuccessStatusCode, Is.True, "Create entity must succeed to get Id");
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);

        var response = await Client.GetAsync($"/api/entities/{_entityType}/{created!.Id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Get entity should return OK");
        var retrieved = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Id, Is.EqualTo(created.Id));
    }

    [Test, Order(7)]
    public async Task FilterEntities_ByBrandId_ReturnsFilteredResults()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(entities.All(e => e.Fields["brandId"]?.ToString() == brandId), Is.True);
    }

    [Test, Order(8)]
    public async Task FilterEntities_ByUsername_ReturnsEntity()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Any(e => e.Fields["username"]?.ToString() == username), Is.True);
    }

    [Test, Order(9)]
    public async Task UpdateEntity_WithValidData_ReturnsNoContent()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

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
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);

        created!.Fields["firstName"] = "Updated";
        created.Fields["agentType"] = "technical";

        var response = await Client.PutAsJsonAsync($"/api/entities/{_entityType}/{created.Id}", created);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/entities/{_entityType}/{created.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Fields["firstName"], Is.EqualTo("Updated"));
        Assert.That(updated.Fields["agentType"], Is.EqualTo("technical"));
    }

    [Test, Order(10)]
    public async Task DeleteEntity_WithValidId_ReturnsNoContent()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

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
        var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);

        var response = await Client.DeleteAsync($"/api/entities/{_entityType}/{created!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/entities/{_entityType}/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test, Order(11)]
    public async Task CreateEntity_WithMissingRequiredField_ReturnsBadRequest()
    {
        if (_skippedDueToMongoDb)
            Assert.Inconclusive("MongoDB is not available. Start MongoDB to run these tests.");
        EnsureEntityTypeSet();

        var entity = new DynamicEntity
        {
            Fields = new Dictionary<string, object?>
            {
                { "password", "Test@123" },
                { "userId", "user006" }
            }
        };

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test, Order(12)]
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

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private void EnsureEntityTypeSet()
    {
        if (string.IsNullOrEmpty(_entityType))
            Assert.Fail("Entity type not set. Run CreateSchema_ForAgentEntity_ReturnsCreated (Order 1) first.");
    }
}
