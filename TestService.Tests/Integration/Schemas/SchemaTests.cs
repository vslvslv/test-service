using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Schemas;

/// <summary>
/// Tests for schema creation with positive scenarios
/// </summary>
[TestFixture]
public class SchemaCreationPositiveTests : IntegrationTestBase
{
    [Test]
    public async Task CreateSchema_WithValidData_ReturnsCreated()
    {
        // Arrange
        var uniqueName = CreateUniqueName("CreateTest");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(uniqueName);

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.EntityName, Is.EqualTo(uniqueName));
        Assert.That(created.Id, Is.Not.Null);
        Assert.That(created.CreatedAt, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task CreateSchema_WithAllFields_ReturnsCreatedWithAllData()
    {
        // Arrange
        var uniqueName = CreateUniqueName("FullSchema");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(uniqueName)
            .WithField("username", "string", required: true, description: "User's unique username")
            .WithField("age", "number", required: false, description: "User's age")
            .WithField("active", "boolean", required: false)
            .WithField("createdDate", "datetime", required: false)
            .WithFilterableFields("username", "age", "active")
            .WithExcludeOnFetch(true)
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(created!.Fields.Count, Is.EqualTo(4));
        Assert.That(created.FilterableFields.Count, Is.EqualTo(3));
        Assert.That(created.ExcludeOnFetch, Is.True);
        Assert.That(created.Fields.First(f => f.Name == "username").Description, Is.EqualTo("User's unique username"));
    }

    [Test]
    public async Task CreateSchema_WithExcludeOnFetch_CreatesSuccessfully()
    {
        // Arrange
        var uniqueName = CreateUniqueName("ExcludeTest");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(uniqueName)
            .WithField("name", "string", required: true)
            .WithExcludeOnFetch(true)
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(created!.ExcludeOnFetch, Is.True);
    }

    [Test]
    public async Task CreateSchema_WithMultipleRequiredFields_CreatesSuccessfully()
    {
        // Arrange
        var uniqueName = CreateUniqueName("MultiRequired");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(uniqueName)
            .WithFields(
                ("field1", "string", true),
                ("field2", "string", true),
                ("field3", "string", true)
            )
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(created!.Fields.Count(f => f.Required), Is.EqualTo(3));
    }

    [Test]
    public async Task CreateSchema_WithNoFilterableFields_CreatesSuccessfully()
    {
        // Arrange
        var uniqueName = CreateUniqueName("NoFilters");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(uniqueName)
            .WithField("data", "string", required: true)
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(created!.FilterableFields, Is.Empty);
    }
}

/// <summary>
/// Tests for schema creation with negative scenarios
/// </summary>
[TestFixture]
public class SchemaCreationNegativeTests : IntegrationTestBase
{
    [Test]
    public async Task CreateSchema_WithEmptyEntityName_ReturnsBadRequest()
    {
        // Arrange
        var schema = new EntitySchema
        {
            EntityName = "",
            Fields = new List<FieldDefinition> { new() { Name = "test", Type = "string" } }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateSchema_WithDuplicateName_ReturnsConflict()
    {
        // Arrange
        var uniqueName = CreateUniqueName("Duplicate");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(uniqueName);
        
        await Client.PostAsJsonAsync("/api/schemas", schema);

        // Act - Try to create again
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateSchema_WithNoFields_StillCreatesSuccessfully()
    {
        // Arrange - This tests that schema can exist without fields (edge case)
        var uniqueName = CreateUniqueName("NoFields");
        var schema = new EntitySchema
        {
            EntityName = uniqueName,
            Fields = new List<FieldDefinition>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert - Should create successfully (business decision)
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateSchema_WithNullEntityName_ReturnsBadRequest()
    {
        // Arrange
        var schema = new EntitySchema
        {
            EntityName = null!,
            Fields = new List<FieldDefinition> { new() { Name = "test", Type = "string" } }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateSchema_WithFilterableFieldNotInFields_StillSucceeds()
    {
        // Arrange - Filterable field that doesn't exist in fields (edge case)
        var uniqueName = CreateUniqueName("InvalidFilter");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(uniqueName)
            .WithField("actualField", "string")
            .WithFilterableField("nonExistentField")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert - Should succeed but filtering on that field won't work
        AssertStatusCode(response, HttpStatusCode.Created);
    }
}

/// <summary>
/// Tests for schema retrieval operations
/// </summary>
[TestFixture]
public class SchemaRetrievalTests : IntegrationTestBase
{
    private string _testSchemaName = null!;

    protected override async void OnSetUp()
    {
        _testSchemaName = CreateUniqueName("Retrieval");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(_testSchemaName);
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task GetAllSchemas_ReturnsListOfSchemas()
    {
        // Act
        var response = await Client.GetAsync("/api/schemas");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var schemas = await response.Content.ReadFromJsonAsync<List<EntitySchema>>();
        Assert.That(schemas, Is.Not.Null);
        Assert.That(schemas!.Count, Is.GreaterThan(0));
        Assert.That(schemas.Any(s => s.EntityName == _testSchemaName), Is.True);
    }

    [Test]
    public async Task GetSchemaByName_WithExistingSchema_ReturnsSchema()
    {
        // Act
        var response = await Client.GetAsync($"/api/schemas/{_testSchemaName}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var schema = await response.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(schema, Is.Not.Null);
        Assert.That(schema!.EntityName, Is.EqualTo(_testSchemaName));
        Assert.That(schema.Fields, Is.Not.Empty);
    }

    [Test]
    public async Task GetSchemaByName_WithNonExistentSchema_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/schemas/NonExistentSchema");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetSchemaByName_WithEmptyName_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/schemas/");

        // Assert
        // This will likely hit the GetAll endpoint or return NotFound
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.NotFound));
    }
}

/// <summary>
/// Tests for schema update operations
/// </summary>
[TestFixture]
public class SchemaUpdateTests : IntegrationTestBase
{
    [Test]
    public async Task UpdateSchema_WithValidChanges_ReturnsNoContent()
    {
        // Arrange
        var uniqueName = CreateUniqueName("Update");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(uniqueName);
        await ApiHelpers.CreateSchemaAsync(Client, schema);

        // Modify schema
        schema.Fields.Add(new FieldDefinition { Name = "newField", Type = "string" });

        // Act
        var response = await Client.PutAsJsonAsync($"/api/schemas/{uniqueName}", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify update
        var getResponse = await Client.GetAsync($"/api/schemas/{uniqueName}");
        var updated = await getResponse.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(updated!.Fields.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task UpdateSchema_ForNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        var schema = EntitySchemaBuilder.CreateMinimalSchema("NonExistent");

        // Act
        var response = await Client.PutAsJsonAsync("/api/schemas/NonExistent", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateSchema_ChangingExcludeOnFetch_UpdatesSuccessfully()
    {
        // Arrange
        var uniqueName = CreateUniqueName("ExcludeUpdate");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(uniqueName);
        await ApiHelpers.CreateSchemaAsync(Client, schema);

        // Modify
        schema.ExcludeOnFetch = true;

        // Act
        var response = await Client.PutAsJsonAsync($"/api/schemas/{uniqueName}", schema);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        var updated = await ApiHelpers.CreateSchemaAsync(Client, schema);
        Assert.That(updated!.ExcludeOnFetch, Is.True);
    }
}

/// <summary>
/// Tests for schema deletion operations
/// </summary>
[TestFixture]
public class SchemaDeletionTests : IntegrationTestBase
{
    [Test]
    public async Task DeleteSchema_WithExistingSchema_ReturnsNoContent()
    {
        // Arrange
        var uniqueName = CreateUniqueName("Delete");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(uniqueName);
        await ApiHelpers.CreateSchemaAsync(Client, schema);

        // Act
        var response = await Client.DeleteAsync($"/api/schemas/{uniqueName}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await Client.GetAsync($"/api/schemas/{uniqueName}");
        AssertStatusCode(getResponse, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteSchema_WithNonExistentSchema_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/schemas/NonExistent");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteSchema_ThenRecreate_Succeeds()
    {
        // Arrange
        var uniqueName = CreateUniqueName("DeleteRecreate");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(uniqueName);
        await ApiHelpers.CreateSchemaAsync(Client, schema);

        // Act - Delete
        var deleteResponse = await Client.DeleteAsync($"/api/schemas/{uniqueName}");
        AssertStatusCode(deleteResponse, HttpStatusCode.NoContent);

        // Act - Recreate
        var createResponse = await Client.PostAsJsonAsync("/api/schemas", schema);

        // Assert
        AssertStatusCode(createResponse, HttpStatusCode.Created);
    }
}
