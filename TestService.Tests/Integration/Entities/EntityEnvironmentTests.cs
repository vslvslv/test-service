using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Entities;

/// <summary>
/// Tests for entity operations with environment support
/// </summary>
[TestFixture]
public class EntityEnvironmentTests : IntegrationTestBase
{
    private const string TestEntityType = "EnvironmentTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithField("category", "string")
            .WithFilterableFields("name", "category")
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    [Test]
    public async Task CreateEntity_WithDevEnvironment_StoresEnvironment()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Dev Entity")
            .WithEnvironment("dev")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Environment, Is.EqualTo("dev"));
    }

    [Test]
    public async Task CreateEntity_WithoutEnvironment_AllowsCreation()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Global Entity")
            .Build();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/entities/{TestEntityType}", entity);

        // Assert
        AssertStatusCode(response, HttpStatusCode.Created);
        
        var created = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Environment, Is.Null);
    }

    [Test]
    public async Task GetAllEntities_FilterByDevEnvironment_ReturnsOnlyDevEntities()
    {
        // Arrange - Create entities in different environments
        var devEntity = new DynamicEntityBuilder()
            .WithField("name", "Dev Entity 1")
            .WithEnvironment("dev")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);

        var stagingEntity = new DynamicEntityBuilder()
            .WithField("name", "Staging Entity 1")
            .WithEnvironment("staging")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);

        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=dev");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.All(e => e.Environment == "dev"), Is.True);
    }

    [Test]
    public async Task CreateMultipleEntities_InDifferentEnvironments_MaintainsIsolation()
    {
        // Arrange & Act - Create 5 entities in each environment
        var environments = new[] { "dev", "staging", "production" };
        var entityCounts = new Dictionary<string, int>();

        foreach (var env in environments)
        {
            for (int i = 0; i < 5; i++)
            {
                var entity = new DynamicEntityBuilder()
                    .WithField("name", $"{env}_entity_{i}")
                    .WithEnvironment(env)
                    .Build();
                await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);
            }
            entityCounts[env] = 5;
        }

        // Assert - Verify each environment has correct count
        foreach (var env in environments)
        {
            var response = await Client.GetAsync($"/api/entities/{TestEntityType}?environment={env}");
            var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
            
            Assert.That(entities, Is.Not.Null);
            Assert.That(entities!.Count, Is.GreaterThanOrEqualTo(entityCounts[env]));
            Assert.That(entities.All(e => e.Environment == env), Is.True);
        }
    }

    [Test]
    public async Task UpdateEntity_CanChangeEnvironment()
    {
        // Arrange
        var entity = new DynamicEntityBuilder()
            .WithField("name", "Move Entity")
            .WithEnvironment("dev")
            .Build();
        var created = await ApiHelpers.CreateEntityAsync(Client, TestEntityType, entity);

        // Act - Change environment
        created!.Environment = "staging";
        var response = await Client.PutAsJsonAsync($"/api/entities/{TestEntityType}/{created.Id}", created);

        // Assert
        AssertStatusCode(response, HttpStatusCode.NoContent);

        // Verify - Should now be in staging
        var devResponse = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=dev");
        var devEntities = await devResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(devEntities!.Any(e => e.Id == created.Id), Is.False);

        var stagingResponse = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=staging");
        var stagingEntities = await stagingResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(stagingEntities!.Any(e => e.Id == created.Id), Is.True);
    }

    [Test]
    public async Task GetAllEntities_WithoutEnvironmentFilter_ReturnsAllEntities()
    {
        // Arrange
        var devCount = 0;
        var stagingCount = 0;
        
        for (int i = 0; i < 3; i++)
        {
            var devEntity = new DynamicEntityBuilder()
                .WithField("name", $"Dev All {i}")
                .WithEnvironment("dev")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);
            devCount++;

            var stagingEntity = new DynamicEntityBuilder()
                .WithField("name", $"Staging All {i}")
                .WithEnvironment("staging")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);
            stagingCount++;
        }

        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.Count, Is.GreaterThanOrEqualTo(devCount + stagingCount));
    }

    [Test]
    public async Task FilterEntities_ByFieldAndEnvironment_ReturnsCorrectSubset()
    {
        // Arrange - Create entities with same category in different environments
        var devEntity1 = new DynamicEntityBuilder()
            .WithField("name", "Dev Cat 1")
            .WithField("category", "test-category")
            .WithEnvironment("dev")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity1);

        var devEntity2 = new DynamicEntityBuilder()
            .WithField("name", "Dev Cat 2")
            .WithField("category", "test-category")
            .WithEnvironment("dev")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity2);

        var stagingEntity = new DynamicEntityBuilder()
            .WithField("name", "Staging Cat 1")
            .WithField("category", "test-category")
            .WithEnvironment("staging")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);

        // Act - Filter by category in dev environment
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/filter/category/test-category?environment=dev");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entities = await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(entities, Is.Not.Null);
        Assert.That(entities!.All(e => e.Environment == "dev"), Is.True);
        Assert.That(entities.All(e => GetFieldString(e, "category") == "test-category"), Is.True);
    }

    [Test]
    public async Task ResetAllConsumed_ByEnvironment_OnlyResetsSpecifiedEnvironment()
    {
        // Arrange - Create and consume entities in both environments
        for (int i = 0; i < 3; i++)
        {
            var devEntity = new DynamicEntityBuilder()
                .WithField("name", $"Dev Reset {i}")
                .WithEnvironment("dev")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);
            await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");

            var stagingEntity = new DynamicEntityBuilder()
                .WithField("name", $"Staging Reset {i}")
                .WithEnvironment("staging")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);
            await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=staging");
        }

        // Act - Reset only dev environment
        var response = await Client.PostAsync($"/api/entities/{TestEntityType}/reset-all?environment=dev", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(Convert.ToInt32(result!["resetCount"]), Is.GreaterThanOrEqualTo(3));

        // Verify - Dev entities should be available, staging still consumed
        var devResponse = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=dev");
        var devEntities = await devResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(devEntities!.Count, Is.GreaterThanOrEqualTo(3));

        var stagingResponse = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=staging");
        var stagingEntities = await stagingResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(stagingEntities!.Count, Is.EqualTo(0)); // All still consumed
    }

    [Test]
    public async Task GetNextAvailable_WhenEnvironmentExhausted_OtherEnvironmentsUnaffected()
    {
        // Arrange - Create 2 entities in dev, 5 in staging
        for (int i = 0; i < 2; i++)
        {
            var devEntity = new DynamicEntityBuilder()
                .WithField("name", $"Dev Limited {i}")
                .WithEnvironment("dev")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);
        }

        for (int i = 0; i < 5; i++)
        {
            var stagingEntity = new DynamicEntityBuilder()
                .WithField("name", $"Staging Plenty {i}")
                .WithEnvironment("staging")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);
        }

        // Act - Exhaust dev environment
        await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");
        await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");
        var devExhausted = await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");

        // Assert - Dev is exhausted
        AssertStatusCode(devExhausted, HttpStatusCode.NotFound);

        // Act - Staging should still work
        var stagingResponse = await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=staging");

        // Assert - Staging still has entities
        AssertStatusCode(stagingResponse, HttpStatusCode.OK);
    }
}

/// <summary>
/// Tests for parallel execution with environment support
/// </summary>
[TestFixture]
public class ParallelExecutionWithEnvironmentTests : IntegrationTestBase
{
    private const string TestEntityType = "ParallelEnvTest";

    protected override async void OnOneTimeSetUp()
    {
        var schema = new EntitySchemaBuilder()
            .WithEntityName(TestEntityType)
            .WithField("name", "string", required: true)
            .WithExcludeOnFetch(true)
            .Build();
        
        await ApiHelpers.CreateSchemaAsync(Client, schema);
    }

    protected override async void OnSetUp()
    {
        // Reset all consumed entities before each test
        await Client.PostAsync($"/api/entities/{TestEntityType}/reset-all?environment=dev", null);
        await Client.PostAsync($"/api/entities/{TestEntityType}/reset-all?environment=staging", null);
    }

    [Test]
    public async Task GetNextAvailable_FilterByDevEnvironment_ReturnsDevEntity()
    {
        // Arrange
        var devEntity = new DynamicEntityBuilder()
            .WithField("name", "Dev Next 1")
            .WithEnvironment("dev")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);

        var stagingEntity = new DynamicEntityBuilder()
            .WithField("name", "Staging Next 1")
            .WithEnvironment("staging")
            .Build();
        await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);

        // Act
        var response = await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var entity = await response.Content.ReadFromJsonAsync<DynamicEntity>();
        Assert.That(entity, Is.Not.Null);
        Assert.That(entity!.Environment, Is.EqualTo("dev"));
        Assert.That(entity.IsConsumed, Is.True);
    }

    [Test]
    public async Task GetNextAvailable_ParallelRequestsDifferentEnvironments_NoConflict()
    {
        // Arrange - Create entities in dev and staging
        for (int i = 0; i < 5; i++)
        {
            var devEntity = new DynamicEntityBuilder()
                .WithField("name", $"Dev Parallel {i}")
                .WithEnvironment("dev")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);

            var stagingEntity = new DynamicEntityBuilder()
                .WithField("name", $"Staging Parallel {i}")
                .WithEnvironment("staging")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);
        }

        // Act - Simulate parallel requests to different environments
        var devTask1 = Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");
        var devTask2 = Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");
        var stagingTask1 = Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=staging");
        var stagingTask2 = Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=staging");

        await Task.WhenAll(devTask1, devTask2, stagingTask1, stagingTask2);

        // Assert - All requests successful
        Assert.That(devTask1.Result.IsSuccessStatusCode, Is.True);
        Assert.That(devTask2.Result.IsSuccessStatusCode, Is.True);
        Assert.That(stagingTask1.Result.IsSuccessStatusCode, Is.True);
        Assert.That(stagingTask2.Result.IsSuccessStatusCode, Is.True);

        // Extract entities
        var devEntity1 = await devTask1.Result.Content.ReadFromJsonAsync<DynamicEntity>();
        var devEntity2 = await devTask2.Result.Content.ReadFromJsonAsync<DynamicEntity>();
        var stagingEntity1 = await stagingTask1.Result.Content.ReadFromJsonAsync<DynamicEntity>();
        var stagingEntity2 = await stagingTask2.Result.Content.ReadFromJsonAsync<DynamicEntity>();

        // Assert - All IDs are unique
        var ids = new[] { devEntity1!.Id, devEntity2!.Id, stagingEntity1!.Id, stagingEntity2!.Id };
        Assert.That(ids.Distinct().Count(), Is.EqualTo(4));

        // Assert - Environments are correct
        Assert.That(devEntity1.Environment, Is.EqualTo("dev"));
        Assert.That(devEntity2.Environment, Is.EqualTo("dev"));
        Assert.That(stagingEntity1.Environment, Is.EqualTo("staging"));
        Assert.That(stagingEntity2.Environment, Is.EqualTo("staging"));
    }

    [Test]
    public async Task ResetAllConsumed_ByEnvironment_OnlyResetsSpecifiedEnvironment()
    {
        // Arrange - Create and consume entities in both environments
        for (int i = 0; i < 3; i++)
        {
            var devEntity = new DynamicEntityBuilder()
                .WithField("name", $"Dev Reset {i}")
                .WithEnvironment("dev")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);
            await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");

            var stagingEntity = new DynamicEntityBuilder()
                .WithField("name", $"Staging Reset {i}")
                .WithEnvironment("staging")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);
            await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=staging");
        }

        // Act - Reset only dev environment
        var response = await Client.PostAsync($"/api/entities/{TestEntityType}/reset-all?environment=dev", null);

        // Assert
        AssertStatusCode(response, HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.That(Convert.ToInt32(result!["resetCount"]), Is.GreaterThanOrEqualTo(3));

        // Verify - Dev entities should be available, staging still consumed
        var devResponse = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=dev");
        var devEntities = await devResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(devEntities!.Count, Is.GreaterThanOrEqualTo(3));

        var stagingResponse = await Client.GetAsync($"/api/entities/{TestEntityType}?environment=staging");
        var stagingEntities = await stagingResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(stagingEntities!.Count, Is.EqualTo(0)); // All still consumed
    }

    [Test]
    public async Task GetNextAvailable_WhenEnvironmentExhausted_OtherEnvironmentsUnaffected()
    {
        // Arrange - Create 2 entities in dev, 5 in staging
        for (int i = 0; i < 2; i++)
        {
            var devEntity = new DynamicEntityBuilder()
                .WithField("name", $"Dev Limited {i}")
                .WithEnvironment("dev")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, devEntity);
        }

        for (int i = 0; i < 5; i++)
        {
            var stagingEntity = new DynamicEntityBuilder()
                .WithField("name", $"Staging Plenty {i}")
                .WithEnvironment("staging")
                .Build();
            await ApiHelpers.CreateEntityAsync(Client, TestEntityType, stagingEntity);
        }

        // Act - Exhaust dev environment
        await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");
        await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");
        var devExhausted = await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=dev");

        // Assert - Dev is exhausted
        AssertStatusCode(devExhausted, HttpStatusCode.NotFound);

        // Act - Staging should still work
        var stagingResponse = await Client.GetAsync($"/api/entities/{TestEntityType}/next?environment=staging");

        // Assert - Staging still has entities
        AssertStatusCode(stagingResponse, HttpStatusCode.OK);
    }
}
