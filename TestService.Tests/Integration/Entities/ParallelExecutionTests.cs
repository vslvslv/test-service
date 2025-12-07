using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

/// <summary>
/// Tests for the ExcludeOnFetch feature (parallel test execution)
/// </summary>
[TestFixture]
public class ParallelExecutionTests : IntegrationTestBase
{
    private const string TestEntityType = "ParallelTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithField("testId", "string")
            .WithFilterableField("testId")
            .WithExcludeOnFetch(true) // Enable exclude on fetch
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    protected override async void OnSetUp()
    {
        // Reset all consumed entities before each test
        await ApiHelpers.ResetAllConsumedAsync(Client, TestEntityType);
    }

    [Test]
    public async Task GetNextAvailable_WithAvailableEntity_ReturnsAndMarksConsumed()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/next");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var retrieved = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.IsConsumed, Is.True);
    }

    [Test]
    public async Task GetNextAvailable_CalledMultipleTimes_ReturnsDifferentEntities()
    {
        // Arrange - Create multiple entities
        for (int i = 0; i < 5; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity_{i}")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }

        // Act - Get next available multiple times
        var retrievedIds = new HashSet<string>();
        for (int i = 0; i < 5; i++)
        {
            var response = await Client.GetAsync($"/api/entities/{TestEntityType}/next");
            var entity = await response.Content.ReadFromJsonAsync<DynamicEntity>();
            retrievedIds.Add(entity!.Id!);
        }

        // Assert - All IDs should be unique
        Assert.That(retrievedIds.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task GetNextAvailable_WhenAllConsumed_ReturnsNotFound()
    {
        // Arrange - Create and consume one entity
        var entity = new DynamicEntityBuilder()
            .WithField("name", "OnlyOne")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        
        // Consume it
        await Client.GetAsync($"/api/entities/{TestEntityType}/next");

        // Act - Try to get next when none available
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/next");

        // Assert
        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetNextAvailable_ForSchemaWithoutExcludeOnFetch_ReturnsBadRequest()
    {
        // Arrange - Create schema without excludeOnFetch
        var schemaName = CreateUniqueName("NoExclude");
        var schema = EntitySchemaBuilder.CreateMinimalSchema(schemaName);
        await ApiHelpers.CreateSchemaAsync(Client, schema);

        // Act
        var response = await Client.GetAsync($"/api/entities/{schemaName}/next");

        // Assert
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetAll_WithExcludeOnFetch_ExcludesConsumedEntities()
    {
        // Arrange - Create entities
        for (int i = 0; i < 3; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity_{i}")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }

        // Consume one
        await Client.GetAsync($"/api/entities/{TestEntityType}/next");

        // Act - Get all
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}");
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();

        // Assert - Should return only non-consumed entities
        Assert.That(entities!.Count, Is.EqualTo(2));
        Assert.That(entities.All(e => !e.IsConsumed), Is.True);
    }

    [Test]
    public async Task GetById_WithExcludeOnFetch_MarksEntityAsConsumed()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .Build();
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Act - Get by ID
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/{created!.Id}");
        
        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        var retrieved = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(retrieved!.IsConsumed, Is.True);

        // Verify it's excluded from GetAll
        var allResponse = await Client.GetAsync($"/api/entities/{TestEntityType}");
        var allEntities = await allResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(allEntities!.Any(e => e.Id == created.Id), Is.False);
    }

    [Test]
    public async Task Filter_WithExcludeOnFetch_ExcludesConsumedEntities()
    {
        // Arrange - Create entities with same testId
        var testId = CreateUniqueId();
        for (int i = 0; i < 3; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity_{i}")
                .WithField("testId", testId)
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }

        // Consume one
        await Client.GetAsync($"/api/entities/{TestEntityType}/next");

        // Act - Filter
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/filter/testId/{testId}");
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();

        // Assert - Should return only non-consumed entities
        Assert.That(entities!.Count, Is.EqualTo(2));
        Assert.That(entities.All(e => !e.IsConsumed), Is.True);
    }

    [Test]
    public async Task ResetConsumed_ForSingleEntity_MakesEntityAvailableAgain()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Test")
            .Build();
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Consume it
        await Client.GetAsync($"/api/entities/{TestEntityType}/{created!.Id}");

        // Act - Reset
        var response = await Client.PostAsync($"/api/entities/{TestEntityType}/{created.Id}/reset", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify it's available again
        var allResponse = await Client.GetAsync($"/api/entities/{TestEntityType}");
        var allEntities = await allResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(allEntities!.Any(e => e.Id == created.Id), Is.True);
    }

    [Test]
    public async Task ResetAllConsumed_ResetsMultipleEntities()
    {
        // Arrange - Create and consume multiple entities
        var entityIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity_{i}")
                .Build();
            var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
            entityIds.Add(created!.Id!);
            
            // Consume it
            await Client.GetAsync($"/api/entities/{TestEntityType}/next");
        }

        // Act - Reset all
        var response = await Client.PostAsync($"/api/entities/{TestEntityType}/reset-all", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(result!["resetCount"], Is.EqualTo(3));

        // Verify all are available again
        var allResponse = await Client.GetAsync($"/api/entities/{TestEntityType}");
        var allEntities = await allResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(allEntities!.Count, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    [Explicit("This test simulates actual parallel execution and may be slow")]
    public async Task SimulateParallelTests_GetNextAvailable_NoConflicts()
    {
        // Arrange - Create 10 entities
        for (int i = 0; i < 10; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"ParallelEntity_{i}")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }

        // Act - Simulate 10 parallel tests
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(GetNextEntityIdAsync());
        }

        var entityIds = await Task.WhenAll(tasks);

        // Assert - All retrieved IDs should be unique
        Assert.That(entityIds.Distinct().Count(), Is.EqualTo(10));
        Assert.That(entityIds.All(id => !string.IsNullOrEmpty(id)), Is.True);
    }

    private async Task<string> GetNextEntityIdAsync()
    {
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/next");
        if (response.IsSuccessStatusCode)
        {
            var entity = await response.Content.ReadFromJsonAsync<DynamicEntity>();
            return entity!.Id!;
        }
        return string.Empty;
    }

    [Test]
    public async Task CreateEntity_WithExcludeOnFetch_StartsAsNotConsumed()
    {
        // Arrange & Act
        var entity = new DynamicEntityBuilder()
            .WithField("name", "New Entity")
            .Build();
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Assert
        Assert.That(created!.IsConsumed, Is.False);
    }

    [Test]
    public async Task UpdateEntity_DoesNotChangeConsumedState()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Original")
            .Build();
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Consume it
        await Client.GetAsync($"/api/entities/{TestEntityType}/{created!.Id}");

        // Act - Update
        created.Fields["name"] = "Updated";
        await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert - Should still be consumed
        var updated = await ApiHelpers.GetEntityByIdAsync(Client, TestEntityType, created.Id!);
        Assert.That(updated, Is.Null); // Because it's consumed and excluded
    }
}

/// <summary>
/// Stress tests for parallel execution (marked as Explicit)
/// </summary>
[TestFixture]
[Explicit("Stress tests - run manually")]
public class ParallelExecutionStressTests : IntegrationTestBase
{
    private const string TestEntityType = "StressTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithExcludeOnFetch(true)
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task StressTest_100ParallelRequests_AllSucceed()
    {
        // Arrange - Create 100 entities
        for (int i = 0; i < 100; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"StressEntity_{i}")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
        }

        // Act - 100 parallel requests
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Client.GetAsync($"/api/entities/{TestEntityType}/next"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert
        var successfulResponses = responses.Count(r => r.IsSuccessStatusCode);
        Assert.That(successfulResponses, Is.EqualTo(100));

        // Extract all IDs
        var entityIds = new List<string>();
        foreach (var response in responses)
        {
            if (response.IsSuccessStatusCode)
            {
                var entity = await response.Content.ReadFromJsonAsync<DynamicEntity>();
                entityIds.Add(entity!.Id!);
            }
        }

        // All IDs should be unique
        Assert.That(entityIds.Distinct().Count(), Is.EqualTo(100));
    }

    [Test]
    public async Task StressTest_ResetAll_HandlesLargeDataset()
    {
        // Arrange - Create and consume 50 entities
        for (int i = 0; i < 50; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"ResetEntity_{i}")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
            await Client.GetAsync($"/api/entities/{TestEntityType}/next");
        }

        // Act
        var response = await Client.PostAsync($"/api/entities/{TestEntityType}/reset-all", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(Convert.ToInt32(result!["resetCount"]), Is.GreaterThanOrEqualTo(50));
    }
}
