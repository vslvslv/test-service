using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

/// <summary>
/// Tests for single unique field constraint (e.g., username must be unique)
/// </summary>
[TestFixture]
public class EntitySingleUniqueFieldTests : IntegrationTestBase
{
    private const string TestEntityType = "SingleUniqueTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("username", "string", required: true)
            .WithField("email", "string", required: true)
            .WithField("brandId", "string")
            .WithFilterableField("username")
            .WithUniqueField("username")  // Username must be unique
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithUniqueUsername_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("username", $"unique_user_{Guid.NewGuid()}")
            .WithField("email", "user@example.com")
            .WithField("brandId", "brand123")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange - Create first entity
        var username = $"duplicate_{Guid.NewGuid()}";
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", username)
            .WithField("email", "user1@example.com")
            .WithField("brandId", "brand123")
            .Build();
        
        var createResponse = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity1);
        AssertStatusCode(createResponse, HttpStatusCode.Created);

        // Act - Try to create duplicate
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", username)  // Same username
            .WithField("email", "user2@example.com")  // Different email
            .WithField("brandId", "brand456")
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
        
        var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(errorResponse, Is.Not.Null);
        Assert.That(errorResponse!.ContainsKey("error"), Is.True);
        Assert.That(errorResponse["error"].ToString(), Is.EqualTo("DUPLICATE_ENTITY"));
        Assert.That(errorResponse["field"].ToString(), Is.EqualTo("username"));
        Assert.That(errorResponse["value"].ToString(), Is.EqualTo(username));
    }

    [Test]
    public async Task CreateEntity_WithSameEmailButDifferentUsername_Succeeds()
    {
        // Arrange - Only username is unique, email can be duplicate
        var email = $"shared_{Guid.NewGuid()}@example.com";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .WithField("email", email)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Same email, different username
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", $"user2_{Guid.NewGuid()}")
            .WithField("email", email)  // Same email
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert - Should succeed because only username is unique
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task DeleteAndRecreate_WithSameUsername_Succeeds()
    {
        // Arrange - Create entity
        var username = $"delete_recreate_{Guid.NewGuid()}";
        var entity = new DynamicEntityBuilder()
            .WithField("username", username)
            .WithField("email", "user@example.com")
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        Assert.That(created, Is.Not.Null);

        // Act - Delete
        var deleteResponse = await Client.DeleteAsync($"/api/entities/{TestEntityType}/{created!.Id}");
        AssertStatusCode(deleteResponse, HttpStatusCode.NoContent);

        // Act - Recreate with same username
        var recreated = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert - Should succeed
        AssertStatusCode(recreated, HttpStatusCode.Created);
    }

    [Test]
    public async Task UpdateEntity_ToExistingUsername_ReturnsConflict()
    {
        // Arrange - Create two entities
        var username1 = $"user1_{Guid.NewGuid()}";
        var username2 = $"user2_{Guid.NewGuid()}";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", username1)
            .WithField("email", "user1@example.com")
            .Build();
        
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", username2)
            .WithField("email", "user2@example.com")
            .Build();
        
        var created1 = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity2);

        // Act - Try to update entity1 to use username2
        created1!.Fields["username"] = username2;
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created1.Id}", created1);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task UpdateEntity_KeepingSameUsername_Succeeds()
    {
        // Arrange - Create entity
        var username = $"update_same_{Guid.NewGuid()}";
        var entity = new DynamicEntityBuilder()
            .WithField("username", username)
            .WithField("email", "original@example.com")
            .WithField("brandId", "brand123")
            .Build();
        
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Act - Update other fields, keep username
        created!.Fields["email"] = "updated@example.com";
        created.Fields["brandId"] = "brand456";
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert - Should succeed
        AssertStatusCode(response, HttpStatusCode.NoContent);
    }
}

/// <summary>
/// Tests for multiple unique fields (username AND email must be unique)
/// </summary>
[TestFixture]
public class EntityMultipleUniqueFieldsTests : IntegrationTestBase
{
    private const string TestEntityType = "MultipleUniqueTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("username", "string", required: true)
            .WithField("email", "string", required: true)
            .WithField("phone", "string")
            .WithFilterableFields("username", "email")
            .WithUniqueFields("username", "email")  // Both must be unique
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithUniqueBothFields_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("username", $"user_{Guid.NewGuid()}")
            .WithField("email", $"user_{Guid.NewGuid()}@example.com")
            .WithField("phone", "123-456-7890")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange - Create first entity
        var username = $"dup_user_{Guid.NewGuid()}";
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", username)
            .WithField("email", $"email1_{Guid.NewGuid()}@example.com")
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Try duplicate username
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", username)  // Duplicate
            .WithField("email", $"email2_{Guid.NewGuid()}@example.com")  // Unique
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
        
        var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(errorResponse!["field"].ToString(), Is.EqualTo("username"));
    }

    [Test]
    public async Task CreateEntity_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange - Create first entity
        var email = $"dup_email_{Guid.NewGuid()}@example.com";
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .WithField("email", email)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Try duplicate email
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", $"user2_{Guid.NewGuid()}")  // Unique
            .WithField("email", email)  // Duplicate
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
        
        var errorResponse = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(errorResponse!["field"].ToString(), Is.EqualTo("email"));
    }

    [Test]
    public async Task CreateEntity_WithDuplicatePhoneButUniqueUsernameEmail_Succeeds()
    {
        // Arrange - Create first entity
        var phone = "555-1234";
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", $"user1_{Guid.NewGuid()}")
            .WithField("email", $"user1_{Guid.NewGuid()}@example.com")
            .WithField("phone", phone)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Same phone, different username/email
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", $"user2_{Guid.NewGuid()}")
            .WithField("email", $"user2_{Guid.NewGuid()}@example.com")
            .WithField("phone", phone)  // Same phone
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert - Should succeed (phone is not unique)
        AssertStatusCode(response, HttpStatusCode.Created);
    }
}

/// <summary>
/// Tests for compound unique constraint (combination of fields must be unique)
/// </summary>
[TestFixture]
public class EntityCompoundUniqueTests : IntegrationTestBase
{
    private const string TestEntityType = "CompoundUniqueTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("brandId", "string", required: true)
            .WithField("agentId", "string", required: true)
            .WithField("region", "string")
            .WithFilterableFields("brandId", "agentId")
            .WithUniqueFields("brandId", "agentId")  // Combination must be unique
            .WithCompoundUnique(true)
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithUniqueCompoundKey_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("brandId", $"brand_{Guid.NewGuid()}")
            .WithField("agentId", $"agent_{Guid.NewGuid()}")
            .WithField("region", "US")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithDuplicateCompoundKey_ReturnsConflict()
    {
        // Arrange - Create first entity
        var brandId = $"brand_{Guid.NewGuid()}";
        var agentId = $"agent_{Guid.NewGuid()}";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("brandId", brandId)
            .WithField("agentId", agentId)
            .WithField("region", "US")
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Try same combination
        var entity2 = new DynamicEntityBuilder()
            .WithField("brandId", brandId)
            .WithField("agentId", agentId)
            .WithField("region", "EU")  // Different region doesn't matter
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateEntity_WithSameBrandDifferentAgent_Succeeds()
    {
        // Arrange - Create first entity
        var brandId = $"shared_brand_{Guid.NewGuid()}";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("brandId", brandId)
            .WithField("agentId", $"agent1_{Guid.NewGuid()}")
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Same brand, different agent
        var entity2 = new DynamicEntityBuilder()
            .WithField("brandId", brandId)  // Same
            .WithField("agentId", $"agent2_{Guid.NewGuid()}")  // Different
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert - Should succeed
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithSameAgentDifferentBrand_Succeeds()
    {
        // Arrange - Create first entity
        var agentId = $"shared_agent_{Guid.NewGuid()}";
        
        var entity1 = new DynamicEntityBuilder()
            .WithField("brandId", $"brand1_{Guid.NewGuid()}")
            .WithField("agentId", agentId)
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Different brand, same agent
        var entity2 = new DynamicEntityBuilder()
            .WithField("brandId", $"brand2_{Guid.NewGuid()}")  // Different
            .WithField("agentId", agentId)  // Same
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert - Should succeed
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateMultipleEntities_WithDifferentCombinations_AllSucceed()
    {
        // Arrange & Act - Create multiple entities with different combinations
        var combinations = new[]
        {
            ("brand1", "agent1"),
            ("brand1", "agent2"),
            ("brand2", "agent1"),
            ("brand2", "agent2")
        };

        foreach (var (brandId, agentId) in combinations)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("brandId", $"{brandId}_{Guid.NewGuid()}")
                .WithField("agentId", $"{agentId}_{Guid.NewGuid()}")
                .Build();
            
            var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);
            
            // Assert
            AssertStatusCode(response, HttpStatusCode.Created);
        }
    }
}

/// <summary>
/// Tests for edge cases and special scenarios
/// </summary>
[TestFixture]
public class EntityDuplicateEdgeCaseTests : IntegrationTestBase
{
    private const string TestEntityType = "EdgeCaseTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("username", "string", required: true)
            .WithField("email", "string")
            .WithField("optional", "string")
            .WithUniqueField("username")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithCaseSensitiveUsername_CreatesMultiple()
    {
        // MongoDB indexes are case-sensitive by default
        // Arrange
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", "TestUser")
            .WithField("email", "test1@example.com")
            .Build();
        
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", "testuser")  // Different case
            .WithField("email", "test2@example.com")
            .Build();

        // Act
        var response1 = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity1);
        var response2 = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert - Both should succeed (case-sensitive)
        AssertStatusCode(response1, HttpStatusCode.Created);
        AssertStatusCode(response2, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithNullNonUniqueField_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("username", $"user_{Guid.NewGuid()}")
            .WithField("email", null)  // Null non-unique field
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithSpecialCharactersInUniqueField_Succeeds()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("username", $"user+special_{Guid.NewGuid()}@test.com")
            .WithField("email", "email@example.com")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateEntity_WithWhitespaceInUniqueField_PreservesExactValue()
    {
        // Arrange
        var username = $"  user with spaces  _{Guid.NewGuid()}";
        var entity1 = new DynamicEntityBuilder()
            .WithField("username", username)
            .WithField("email", "test@example.com")
            .Build();
        
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity1);

        // Act - Try same username (exact whitespace)
        var entity2 = new DynamicEntityBuilder()
            .WithField("username", username)
            .WithField("email", "test2@example.com")
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity2);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateEntity_WithVeryLongUniqueValue_HandlesCorrectly()
    {
        // Arrange - Create long username (MongoDB handles strings up to 16MB)
        var longUsername = new string('a', 1000) + Guid.NewGuid().ToString();
        var entity = new DynamicEntityBuilder()
            .WithField("username", longUsername)
            .WithField("email", "test@example.com")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
    }
}

/// <summary>
/// Performance and stress tests for duplicate detection
/// </summary>
[TestFixture]
[Explicit("Performance tests - run manually")]
public class EntityDuplicatePerformanceTests : IntegrationTestBase
{
    private const string TestEntityType = "PerformanceTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("username", "string", required: true)
            .WithField("email", "string", required: true)
            .WithUniqueField("username")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateMultipleEntities_VerifyUniqueConstraintPerformance()
    {
        // Arrange & Act - Create 100 entities
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < 100; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("username", $"perf_user_{i}_{Guid.NewGuid()}")
                .WithField("email", $"user{i}@example.com")
                .Build();
            
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }
        
        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(30000)); // Should complete in 30 seconds
        Console.WriteLine($"Created 100 entities in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task DetectDuplicate_AmongManyEntities_PerformsFast()
    {
        // Arrange - Create 50 entities
        for (int i = 0; i < 50; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("username", $"search_user_{i}")
                .WithField("email", $"user{i}@example.com")
                .Build();
            
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }

        // Act - Try to create duplicate
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var duplicate = new DynamicEntityBuilder()
            .WithField("username", "search_user_25")  // Exists
            .WithField("email", "new@example.com")
            .Build();

        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", duplicate);
        stopwatch.Stop();

        // Assert
        AssertStatusCode(response, HttpStatusCode.Conflict);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000)); // Should detect quickly
        Console.WriteLine($"Detected duplicate in {stopwatch.ElapsedMilliseconds}ms");
    }
}
