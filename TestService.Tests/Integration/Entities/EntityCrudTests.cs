using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

/// <summary>
/// Tests for entity creation with positive scenarios
/// </summary>
[TestFixture]
public class EntityCreationPositiveTests : IntegrationTestBase
{
    private const string TestEntityType = "CreationTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithField("description", "string")
            .WithField("value", "number")
            .WithFilterableField("name")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithAllFields_ReturnsCreated()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test Entity")
            .WithField("description", "Test Description")
            .WithField("value", 100)
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null);
        Assert.That(created.EntityType, Is.EqualTo(TestEntityType));
        Assert.That(GetFieldString(created, "name"), Is.EqualTo("Test Entity"));
        Assert.That(created.IsConsumed, Is.False);
    }

    [Test]
    public async Task CreateEntity_WithOnlyRequiredFields_ReturnsCreated()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Minimal Entity")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created!.Fields.ContainsKey("name"), Is.True);
        Assert.That(created.Fields.ContainsKey("description"), Is.False);
    }

    [Test]
    public async Task CreateEntity_MultipleEntities_AllCreatedSuccessfully()
    {
        // Arrange & Act
        var entities = new List<DynamicEntity>();
        for (int i = 0; i < 5; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity {i}")
                .WithField("value", i * 10)
                .Build();
            
            var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
            entities.Add(created!);
        }

        // Assert
        Assert.That(entities.Count, Is.EqualTo(5));
        Assert.That(entities.All(e => e.Id != null), Is.True);
        Assert.That(entities.Select(e => GetFieldString(e, "name")).Distinct().Count(), Is.EqualTo(5));
    }

    [Test]
    public async Task CreateEntity_WithNullOptionalField_CreatesSuccessfully()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .WithField("description", null)
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithSpecialCharactersInFields_CreatesSuccessfully()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test & Entity <with> \"special\" 'characters'")
            .WithField("description", "Special: !@#$%^&*()_+-=[]{}|;':\",./<>?")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(GetFieldString(created!, "name"), Is.EqualTo("Test & Entity <with> \"special\" 'characters'"));
    }
}

/// <summary>
/// Tests for entity creation with negative scenarios
/// </summary>
[TestFixture]
public class EntityCreationNegativeTests : IntegrationTestBase
{
    private const string TestEntityType = "CreationNegativeTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("requiredField", "string", required: true)
            .WithField("optionalField", "string")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithMissingRequiredField_ReturnsBadRequest()
    {
        // Arrange - Missing requiredField
        var entity = new DynamicEntityBuilder()
            .WithField("optionalField", "test")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateEntity_ForNonExistentSchema_ReturnsNotFound()
    {
        // Arrange
        var entity = DynamicEntityBuilder.CreateMinimalEntity("test");

        // Act
        var response = await Client.PostAsJsonAsync("/api/entities/NonExistentType", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateEntity_WithEmptyFields_ReturnsBadRequest()
    {
        // Arrange
        var entity = new DynamicEntity { Fields = new Dictionary<string, object?>() };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateEntity_WithNullRequiredField_ReturnsBadRequest()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("requiredField", null)
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }
}

/// <summary>
/// Tests for entity retrieval operations
/// </summary>
[TestFixture]
public class EntityRetrievalTests : IntegrationTestBase
{
    private const string TestEntityType = "RetrievalTest";
    private List<DynamicEntity> _testEntities = new();

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithField("category", "string")
            .WithField("value", "number")
            .WithFilterableFields("name", "category")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);

        // Create test data
        for (int i = 0; i < 5; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity_{i}")
                .WithField("category", i % 2 == 0 ? "even" : "odd")
                .WithField("value", i * 10)
                .Build();
            
            var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
            _testEntities.Add(created!);
        }
    }

    [Test]
    public async Task GetAllEntities_ReturnsAllEntities()
    {
        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThanOrEqualTo(_testEntities.Count));
    }

    [Test]
    public async Task GetEntityById_WithValidId_ReturnsEntity()
    {
        // Arrange
        var testEntity = _testEntities.First();

        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/{testEntity.Id}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entity = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(entity, Is.Not.Null);
        Assert.That(entity!.Id, Is.EqualTo(testEntity.Id));
        Assert.That(GetFieldString(entity, "name"), Is.EqualTo(GetFieldString(testEntity, "name")));
    }

    [Test]
    public async Task GetEntityById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/507f1f77bcf86cd799439011");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetEntityById_WithMalformedId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/invalid-id");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task FilterEntities_ByCategory_ReturnsFilteredResults()
    {
        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/filter/category/even");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.All(e => GetFieldString(e, "category") == "even"), Is.True);
    }

    [Test]
    public async Task FilterEntities_ByNonFilterableField_ReturnsBadRequest()
    {
        // Act - 'value' is not a filterable field
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/filter/value/10");

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task FilterEntities_WithNoMatches_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/filter/name/NonExistentName");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.EqualTo(0));
    }
}

/// <summary>
/// Tests for entity update operations
/// </summary>
[TestFixture]
public class EntityUpdateTests : IntegrationTestBase
{
    private const string TestEntityType = "UpdateTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithField("description", "string")
            .WithField("value", "number")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task UpdateEntity_WithValidChanges_ReturnsNoContent()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Original")
            .WithField("value", 100)
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Modify
        created!.Fields["name"] = "Updated";
        created.Fields["value"] = 200;

        // Act
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify
        var updated = await ApiHelpers.GetEntityByIdAsync(Client, TestEntityType, created.Id!);
        Assert.That(GetFieldString(updated!, "name"), Is.EqualTo("Updated"));
        Assert.That(GetFieldValue<int>(updated!, "value"), Is.EqualTo(200));
    }

    [Test]
    public async Task UpdateEntity_AddingNewField_UpdatesSuccessfully()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Add new field
        created!.Fields["description"] = "New Description";

        // Act
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        var updated = await ApiHelpers.GetEntityByIdAsync(Client, TestEntityType, created.Id!);
        Assert.That(updated!.Fields.ContainsKey("description"), Is.True);
        Assert.That(GetFieldString(updated, "description"), Is.EqualTo("New Description"));
    }

    [Test]
    public async Task UpdateEntity_RemovingOptionalField_UpdatesSuccessfully()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .WithField("description", "Original")
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Remove optional field
        created!.Fields.Remove("description");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);
    }

    [Test]
    public async Task UpdateEntity_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var entity = DynamicEntityBuilder.CreateMinimalEntity("test");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/507f1f77bcf86cd799439011", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateEntity_RemovingRequiredField_ReturnsBadRequest()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Remove required field
        created!.Fields.Remove("name");

        // Act
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }
}

/// <summary>
/// Tests for entity deletion operations
/// </summary>
[TestFixture]
public class EntityDeletionTests : IntegrationTestBase
{
    private const string TestEntityType = "DeletionTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = EntitySchemaBuilder.CreateMinimalSchema(TestEntityType);
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task DeleteEntity_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var entity = DynamicEntityBuilder.CreateMinimalEntity("ToDelete");
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Act
        var response = await Client.DeleteAsync($"/api/entities/{TestEntityType}/{created!.Id}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify deletion
        var getResponse = await Client.GetAsync($"/api/entities/{TestEntityType}/{created.Id}");
        AssertStatusCode(getResponse, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteEntity_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync($"/api/entities/{TestEntityType}/507f1f77bcf86cd799439011");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteEntity_TwiceWithSameId_SecondReturnsNotFound()
    {
        // Arrange
        var entity = DynamicEntityBuilder.CreateMinimalEntity("ToDeleteTwice");
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Act - First deletion
        var firstResponse = await Client.DeleteAsync($"/api/entities/{TestEntityType}/{created!.Id}");
        AssertStatusCode(firstResponse, HttpStatusCode.NoContent);

        // Act - Second deletion
        var secondResponse = await Client.DeleteAsync($"/api/entities/{TestEntityType}/{created.Id}");

        // Assert
        AssertStatusCode(secondResponse, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteEntity_ThenRecreateWithSameData_Succeeds()
    {
        // Arrange
        var entity = DynamicEntityBuilder.CreateMinimalEntity("ToDeleteAndRecreate");
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        var originalId = created!.Id;

        // Act - Delete
        await Client.DeleteAsync($"/api/entities/{TestEntityType}/{originalId}");

        // Act - Recreate
        var recreated = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Assert
        Assert.That(recreated, Is.Not.Null);
        Assert.That(recreated!.Id, Is.Not.EqualTo(originalId)); // Should have new ID
        Assert.That(GetFieldString(recreated, "name"), Is.EqualTo(GetFieldString(entity, "name")));
    }
}
