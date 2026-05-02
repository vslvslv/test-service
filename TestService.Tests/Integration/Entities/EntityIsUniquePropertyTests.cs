using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

/// <summary>
/// Tests for the isUnique property flag at field level
/// Ensures that fields marked with isUnique=true enforce uniqueness validation
/// </summary>
[TestFixture]
public class EntityIsUniquePropertyTests : IntegrationTestBase
{
    private string _entityType = string.Empty;

    protected override async Task OnOneTimeSetUp()
    {
        _entityType = CreateUniqueName("IsUniquePropertyTest");

        var schema = new EntitySchemaBuilder()
            .WithEntityName(_entityType)
            .WithField("email", "string", required: true, isUnique: true)
            .WithField("username", "string", required: true, isUnique: true)
            .WithField("phone", "string", required: false, isUnique: false)
            .WithField("address", "string")
            .WithFilterableFields("email", "username")
            .WithUniqueFields("email", "username")
            .Build();

        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    protected override async Task OnOneTimeTearDown()
    {
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, _entityType);
    }

    [Test]
    public async Task CreateEntity_WithUniqueFieldValues_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("email", $"unique_{Guid.NewGuid()}@example.com")
            .WithField("username", $"unique_user_{Guid.NewGuid()}")
            .WithField("phone", "555-1234")
            .WithField("address", "123 Main St")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange - Create first entity
        var email = $"duplicate_{Guid.NewGuid()}@example.com";
        var entity1 = new DynamicEntityBuilder()
            .WithField("email", email)
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .WithField("phone", "555-1111")
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Try duplicate email (marked as isUnique=true)
        var entity2 = new DynamicEntityBuilder()
            .WithField("email", email)  // Duplicate
            .WithField("username", $"user2_{Guid.NewGuid()}")  // Different
            .WithField("phone", "555-2222")
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange - Create first entity
        var username = $"duplicate_user_{Guid.NewGuid()}";
        var entity1 = new DynamicEntityBuilder()
            .WithField("email", $"email1_{Guid.NewGuid()}@example.com")
            .WithField("username", username)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Try duplicate username (marked as isUnique=true)
        var entity2 = new DynamicEntityBuilder()
            .WithField("email", $"email2_{Guid.NewGuid()}@example.com")  // Different
            .WithField("username", username)  // Duplicate
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateEntity_WithDuplicatePhone_Succeeds()
    {
        // Arrange - Create first entity
        var phone = "555-9999";
        var entity1 = new DynamicEntityBuilder()
            .WithField("email", $"email1_{Guid.NewGuid()}@example.com")
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .WithField("phone", phone)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Same phone (NOT marked as unique)
        var entity2 = new DynamicEntityBuilder()
            .WithField("email", $"email2_{Guid.NewGuid()}@example.com")
            .WithField("username", $"user2_{Guid.NewGuid()}")
            .WithField("phone", phone)  // Same phone - should be allowed
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert - Should succeed because phone is not unique
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateAddress_Succeeds()
    {
        // Arrange - Create first entity
        var address = "999 Test Street";
        var entity1 = new DynamicEntityBuilder()
            .WithField("email", $"email1_{Guid.NewGuid()}@example.com")
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .WithField("address", address)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Same address (isUnique not specified, defaults to false)
        var entity2 = new DynamicEntityBuilder()
            .WithField("email", $"email2_{Guid.NewGuid()}@example.com")
            .WithField("username", $"user2_{Guid.NewGuid()}")
            .WithField("address", address)  // Same address - should be allowed
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert - Should succeed because address is not marked as unique
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithBothUniqueFieldsDuplicated_ReturnsConflict()
    {
        // Arrange - Create first entity
        var email = $"shared_{Guid.NewGuid()}@example.com";
        var username = $"shared_user_{Guid.NewGuid()}";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("email", email)
            .WithField("username", username)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Try duplicating both unique fields
        var entity2 = new DynamicEntityBuilder()
            .WithField("email", email)  // Duplicate
            .WithField("username", username)  // Duplicate
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert - Should fail due to duplicate (either field triggers conflict)
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateEntity_ChangingUniqueFieldToExistingValue_ReturnsConflict()
    {
        // Arrange - Create two entities
        var email1 = $"email1_{Guid.NewGuid()}@example.com";
        var email2 = $"email2_{Guid.NewGuid()}@example.com";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("email", email1)
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .Build();
        
        var entity2 = new DynamicEntityBuilder()
            .WithField("email", email2)
            .WithField("username", $"user2_{Guid.NewGuid()}")
            .Build();
        
        var created1 = await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);
        var created2 = await ApiHelpers.CreateEntityAsync(Client, _entityType, entity2);

        // Act - Try to change entity2's email to match entity1's email
        created2!.Fields["email"] = email1;
        var response = await Client.PutAsJsonAsync($"/api/entities/{_entityType}/{created2.Id}", created2);

        // Assert - Should fail due to duplicate email
        AssertStatusCode(response, HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        Assert.That(error, Does.Contain("email"));
        Assert.That(error, Does.Contain("must be unique"));
    }

    [Test]
    public async Task UpdateEntity_KeepingSameUniqueValue_Succeeds()
    {
        // Arrange - Create entity
        var email = $"keep_{Guid.NewGuid()}@example.com";
        var entity = new DynamicEntityBuilder()
            .WithField("email", email)
            .WithField("username", $"user_{Guid.NewGuid()}")
            .WithField("phone", "555-1234")
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, _entityType, entity);

        // Act - Update other fields, keep email the same
        created!.Fields["phone"] = "555-9999";
        created.Fields["address"] = "New Address";
        var response = await Client.PutAsJsonAsync($"/api/entities/{_entityType}/{created.Id}", created);

        // Assert - Should succeed (keeping same unique value is allowed)
        AssertStatusCode(response, HttpStatusCode.NoContent);
    }
}

/// <summary>
/// Tests for schemas that use ONLY property-level isUnique flags (without uniqueFields array).
/// Validates that the BsonClassMap registration in Program.cs (SetShouldSerializeMethod) correctly
/// persists IsUnique=true to MongoDB and that the validation logic detects it on read-back.
/// </summary>
[TestFixture]
public class EntityIsUniquePropertyOnlyTests : IntegrationTestBase
{
    private string _entityType = string.Empty;

    protected override async Task OnOneTimeSetUp()
    {
        _entityType = CreateUniqueName("IsUniquePropertyOnlyTest");

        var schema = new EntitySchemaBuilder()
            .WithEntityName(_entityType)
            .WithField("productCode", "string", required: true, isUnique: true)
            .WithField("name", "string", required: true)
            .WithField("category", "string")
            .WithFilterableField("productCode")
            .Build();

        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    protected override async Task OnOneTimeTearDown()
    {
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, _entityType);
    }

    [Test]
    public async Task CreateEntity_WithUniqueProductCode_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("productCode", $"PROD-{Guid.NewGuid()}")
            .WithField("name", "Test Product")
            .WithField("category", "Electronics")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateProductCode_ReturnsConflict()
    {
        // Arrange - Create first entity
        var productCode = $"PROD-{Guid.NewGuid()}";
        var entity1 = new DynamicEntityBuilder()
            .WithField("productCode", productCode)
            .WithField("name", "Product A")
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Try duplicate productCode
        var entity2 = new DynamicEntityBuilder()
            .WithField("productCode", productCode)  // Duplicate
            .WithField("name", "Product B")  // Different name
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert - Should detect uniqueness violation from isUnique property alone
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateName_Succeeds()
    {
        // Arrange - Create first entity
        var name = "Shared Product Name";
        var entity1 = new DynamicEntityBuilder()
            .WithField("productCode", $"PROD-{Guid.NewGuid()}")
            .WithField("name", name)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, _entityType, entity1);

        // Act - Same name, different productCode
        var entity2 = new DynamicEntityBuilder()
            .WithField("productCode", $"PROD-{Guid.NewGuid()}")
            .WithField("name", name)  // Same name - should be allowed
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{_entityType}", entity2);

        // Assert - Should succeed because name is not marked as unique
        AssertStatusCode(response, HttpStatusCode.Created);
    }
}
