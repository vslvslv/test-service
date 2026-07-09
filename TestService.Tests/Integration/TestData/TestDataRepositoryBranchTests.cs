using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.TestDataFeature;

[TestFixture]
public class TestDataRepositoryBranchTests : IntegrationTestBase
{
    [Test]
    public async Task Update_WithNonExistingId_ReturnsNotFound()
    {
        var nonExistingId = "000000000000000000000002";
        var payload = new TestData
        {
            Id = nonExistingId,
            Name = "Ghost",
            Value = 1m,
            Category = "branch-test"
        };

        var response = await Client.PutAsJsonAsync($"/api/testdata/{nonExistingId}", payload);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WithNonExistingId_ReturnsNotFound()
    {
        var nonExistingId = "000000000000000000000003";

        var response = await Client.DeleteAsync($"/api/testdata/{nonExistingId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Create_AssignsCreatedAtAndUpdatedAtTimestamps()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var payload = new TestData
        {
            Name = "TimestampCheck",
            Value = 1m,
            Category = $"ts-{Guid.NewGuid():N}"
        };

        var response = await Client.PostAsJsonAsync("/api/testdata", payload);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await response.Content.ReadFromJsonAsync<TestData>();
        Assert.That(created!.CreatedAt, Is.GreaterThanOrEqualTo(before));
    }

    [Test]
    public async Task GetAggregated_SumsValuesPerCategory()
    {
        var category = $"agg-{Guid.NewGuid():N}";
        await Client.PostAsJsonAsync("/api/testdata", new TestData { Name = "X", Value = 10m, Category = category });
        await Client.PostAsJsonAsync("/api/testdata", new TestData { Name = "Y", Value = 25m, Category = category });

        var response = await Client.GetAsync("/api/testdata/aggregated");
        var dict = await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>();

        Assert.That(dict, Contains.Key(category));
        Assert.That(dict![category], Is.EqualTo(35m));
    }
}
