using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using TestService.Api.Configuration;
using TestService.Api.Models;
using TestService.Api.Services;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

/// <summary>
/// Storage and settings proof for mock log retention: the dated deletes behind the hourly
/// cleanup, and the <c>mockLogRetentionDays</c> default for settings stored before the field existed.
/// </summary>
[TestFixture]
public class MockLogRetentionTests : IntegrationTestBase
{
    private IMongoDatabase Database
    {
        get
        {
            var settings = Factory.Services.GetRequiredService<MongoDbSettings>();
            return new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName);
        }
    }

    private IMockRepository Repository => Factory.Services.GetRequiredService<IMockRepository>();

    [Test]
    public async Task DeleteOlderThan_SevenDays_RemovesOnlyOlderRequestAndCallbackLogs()
    {
        var environment = $"retention-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        // Inserted directly: CreateRequestLogAsync overwrites Timestamp with now.
        var requestLogs = Database.GetCollection<MockRequestLog>("MockRequestLogs");
        await requestLogs.InsertManyAsync(new[]
        {
            new MockRequestLog { Environment = environment, Path = "/old", Timestamp = now.AddDays(-8) },
            new MockRequestLog { Environment = environment, Path = "/recent", Timestamp = now.AddDays(-6) }
        });

        await Repository.CreateCallbackLogAsync(new MockCallbackLog
        {
            Environment = environment, Source = "test", CorrelationId = "old", DueAt = now.AddDays(-8), SentAt = now.AddDays(-8)
        });
        await Repository.CreateCallbackLogAsync(new MockCallbackLog
        {
            Environment = environment, Source = "test", CorrelationId = "recent", DueAt = now.AddDays(-6), SentAt = now.AddDays(-6)
        });

        try
        {
            var cutoff = now.AddDays(-7);
            var deletedRequests = await Repository.DeleteRequestLogsOlderThanAsync(cutoff);
            var deletedCallbacks = await Repository.DeleteCallbackLogsOlderThanAsync(cutoff);

            Assert.That(deletedRequests, Is.GreaterThanOrEqualTo(1));
            Assert.That(deletedCallbacks, Is.GreaterThanOrEqualTo(1));

            var remainingRequests = (await Repository.GetRequestLogsAsync(environment, limit: 100)).ToList();
            Assert.That(remainingRequests.Select(x => x.Path), Is.EquivalentTo(new[] { "/recent" }));

            var remainingCallbacks = (await Repository.GetCallbackLogsAsync(environment, limit: 100)).ToList();
            Assert.That(remainingCallbacks.Select(x => x.CorrelationId), Is.EquivalentTo(new[] { "recent" }));
        }
        finally
        {
            await Repository.DeleteRequestLogsAsync(environment);
            await Repository.DeleteCallbackLogsAsync(environment);
        }
    }

    [Test]
    public async Task Settings_StoredWithoutMockLogRetentionDays_ReadsSevenAndNullRoundTrips()
    {
        // Make sure the singleton settings document exists, then keep the raw original.
        AssertStatusCode(await Client.GetAsync("/api/settings"), HttpStatusCode.OK);
        var collection = Database.GetCollection<BsonDocument>("Settings");
        var filter = Builders<BsonDocument>.Filter.Eq("_id", "app_settings");
        var original = await collection.Find(filter).FirstAsync();

        try
        {
            var legacy = original.DeepClone().AsBsonDocument;
            legacy["DataRetention"].AsBsonDocument.Remove("MockLogRetentionDays");
            await collection.ReplaceOneAsync(filter, legacy);

            var legacyResponse = await Client.GetAsync("/api/settings");
            AssertStatusCode(legacyResponse, HttpStatusCode.OK);
            Assert.That(await ReadMockLogRetentionDaysAsync(legacyResponse), Is.EqualTo(7));

            var current = (await legacyResponse.Content.ReadFromJsonAsync<AppSettings>())!;
            current.DataRetention.MockLogRetentionDays = null;
            var putResponse = await Client.PutAsJsonAsync("/api/settings", current);
            AssertStatusCode(putResponse, HttpStatusCode.OK);

            var afterResponse = await Client.GetAsync("/api/settings");
            AssertStatusCode(afterResponse, HttpStatusCode.OK);
            Assert.That(await ReadMockLogRetentionDaysAsync(afterResponse), Is.Null);
        }
        finally
        {
            await collection.ReplaceOneAsync(filter, original);
        }
    }

    private static async Task<int?> ReadMockLogRetentionDaysAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var value = doc.RootElement.GetProperty("dataRetention").GetProperty("mockLogRetentionDays");
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();
    }
}
