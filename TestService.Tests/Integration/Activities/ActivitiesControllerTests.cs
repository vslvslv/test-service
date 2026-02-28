using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Services;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Activities;

[TestFixture]
public class ActivitiesControllerTests : IntegrationTestBase
{
    protected override async Task OnSetUp()
    {
        await ClearActivitiesAsync();
        await SeedActivitiesAsync();
    }

    [Test]
    public async Task GetActivities_WithFiltersAndPaging_ReturnsExpectedResult()
    {
        var response = await Client.GetAsync("/api/activities?entityType=order&type=entity&action=created&user=alice&skip=0&limit=1");
        AssertStatusCode(response, HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(payload, Is.Not.Null);

        var activities = payload!["activities"]?.AsArray();
        Assert.That(activities, Is.Not.Null);
        Assert.That(activities!.Count, Is.EqualTo(1));

        Assert.That(payload["skip"]?.GetValue<int>(), Is.EqualTo(0));
        Assert.That(payload["limit"]?.GetValue<int>(), Is.EqualTo(1));
        Assert.That(payload["totalCount"]?.GetValue<int>(), Is.EqualTo(3));
    }

    [Test]
    public async Task GetActivities_WithLimitOver500_ClampsLimit()
    {
        var response = await Client.GetAsync("/api/activities?limit=999");
        AssertStatusCode(response, HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!["limit"]?.GetValue<int>(), Is.EqualTo(500));
    }

    [Test]
    public async Task GetRecent_WithExcessiveHoursAndLimit_ReturnsResults()
    {
        var response = await Client.GetAsync("/api/activities/recent?hours=999&limit=999");
        AssertStatusCode(response, HttpStatusCode.OK);

        var activities = await response.Content.ReadFromJsonAsync<List<Activity>>();
        Assert.That(activities, Is.Not.Null);
        Assert.That(activities!.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetStats_ReturnsExpectedAggregations()
    {
        var startDate = DateTime.UtcNow.AddHours(-1).ToString("O");
        var endDate = DateTime.UtcNow.AddHours(1).ToString("O");

        var response = await Client.GetAsync($"/api/activities/stats?startDate={Uri.EscapeDataString(startDate)}&endDate={Uri.EscapeDataString(endDate)}");
        AssertStatusCode(response, HttpStatusCode.OK);

        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(payload, Is.Not.Null);

        Assert.That(payload!["totalActivities"]?.GetValue<int>(), Is.EqualTo(3));
        Assert.That(payload["byType"]?["entity"]?.GetValue<int>(), Is.EqualTo(2));
        Assert.That(payload["byType"]?["environment"]?.GetValue<int>(), Is.EqualTo(1));
        Assert.That(payload["byAction"]?["created"]?.GetValue<int>(), Is.EqualTo(2));
        Assert.That(payload["byAction"]?["updated"]?.GetValue<int>(), Is.EqualTo(1));
        Assert.That(payload["byEntityType"]?["order"]?.GetValue<int>(), Is.EqualTo(2));
        Assert.That(payload["byUser"]?["alice"]?.GetValue<int>(), Is.EqualTo(2));
        Assert.That(payload["byUser"]?["bob"]?.GetValue<int>(), Is.EqualTo(1));
    }

    private async Task ClearActivitiesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var collection = database.GetCollection<Activity>("activities");
        await collection.DeleteManyAsync(Builders<Activity>.Filter.Empty);
    }

    private async Task SeedActivitiesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IActivityRepository>();

        await repository.CreateAsync(new Activity
        {
            Type = ActivityType.Entity,
            Action = ActivityAction.Created,
            User = "alice",
            EntityType = "order",
            EntityId = "o1",
            Description = "Created order 1",
            Environment = "dev"
        });

        await repository.CreateAsync(new Activity
        {
            Type = ActivityType.Entity,
            Action = ActivityAction.Updated,
            User = "bob",
            EntityType = "order",
            EntityId = "o2",
            Description = "Updated order 2",
            Environment = "qa"
        });

        await repository.CreateAsync(new Activity
        {
            Type = ActivityType.Environment,
            Action = ActivityAction.Created,
            User = "alice",
            Description = "Created qa environment",
            Environment = "qa"
        });
    }
}
