using System.Runtime.CompilerServices;
using MongoDB.Driver;
using TestService.Api.Configuration;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IMockRepository
{
    Task<MockExpectation> CreateExpectationAsync(MockExpectation expectation);
    Task<IEnumerable<MockExpectation>> GetExpectationsAsync(string? environment = null, bool includeDisabled = false);
    Task<MockExpectation?> GetExpectationByIdAsync(string id);
    Task<bool> UpdateExpectationAsync(string id, MockExpectation expectation);
    Task<bool> DeleteExpectationAsync(string id);
    Task<bool> TryConsumeOnceAsync(string id);

    Task CreateRequestLogAsync(MockRequestLog requestLog);
    Task<IEnumerable<MockRequestLog>> GetRequestLogsAsync(string? environment = null, string? path = null, int limit = 100, bool? matched = null);
    Task<long> DeleteRequestLogsAsync(string? environment = null);

    /// <summary>Every request log of the environment (all environments when blank), unbounded, streamed from a cursor.</summary>
    IAsyncEnumerable<MockRequestLog> StreamRequestLogsAsync(string? environment, CancellationToken ct = default);

    /// <summary>Deletes request logs whose <c>timestamp</c> is before <paramref name="cutoffUtc"/>.</summary>
    Task<long> DeleteRequestLogsOlderThanAsync(DateTime cutoffUtc);

    Task CreateCallbackLogAsync(MockCallbackLog callbackLog);
    Task<IEnumerable<MockCallbackLog>> GetCallbackLogsAsync(string? environment = null, string? source = null, string? correlationId = null, int limit = 100);
    Task<long> DeleteCallbackLogsAsync(string? environment = null);

    /// <summary>Deletes callback logs whose <c>sentAt</c> is before <paramref name="cutoffUtc"/>.</summary>
    Task<long> DeleteCallbackLogsOlderThanAsync(DateTime cutoffUtc);
}

public class MockRepository : IMockRepository
{
    private readonly IMongoCollection<MockExpectation> _expectationsCollection;
    private readonly IMongoCollection<MockRequestLog> _logsCollection;
    private readonly IMongoCollection<MockCallbackLog> _callbackLogsCollection;

    public MockRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _expectationsCollection = database.GetCollection<MockExpectation>("MockExpectations");
        _logsCollection = database.GetCollection<MockRequestLog>("MockRequestLogs");
        _callbackLogsCollection = database.GetCollection<MockCallbackLog>("MockCallbackLogs");

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            var expectationsIndexes = new[]
            {
                new CreateIndexModel<MockExpectation>(
                    Builders<MockExpectation>.IndexKeys
                        .Ascending(x => x.Environment)
                        .Ascending(x => x.Enabled)
                        .Descending(x => x.Priority)
                        .Ascending(x => x.CreatedAt)
                )
            };
            _expectationsCollection.Indexes.CreateMany(expectationsIndexes);

            var logsIndexes = new[]
            {
                new CreateIndexModel<MockRequestLog>(
                    Builders<MockRequestLog>.IndexKeys
                        .Ascending(x => x.Environment)
                        .Descending(x => x.Timestamp)
                ),
                new CreateIndexModel<MockRequestLog>(
                    Builders<MockRequestLog>.IndexKeys
                        .Ascending(x => x.Path)
                        .Descending(x => x.Timestamp)
                )
            };
            _logsCollection.Indexes.CreateMany(logsIndexes);

            var callbackLogsIndexes = new[]
            {
                new CreateIndexModel<MockCallbackLog>(
                    Builders<MockCallbackLog>.IndexKeys
                        .Ascending(x => x.Environment)
                        .Descending(x => x.SentAt)
                ),
                new CreateIndexModel<MockCallbackLog>(
                    Builders<MockCallbackLog>.IndexKeys
                        .Ascending(x => x.CorrelationId)
                )
            };
            _callbackLogsCollection.Indexes.CreateMany(callbackLogsIndexes);
        }
        catch
        {
            // Index creation may fail when already present.
        }
    }

    public async Task<MockExpectation> CreateExpectationAsync(MockExpectation expectation)
    {
        expectation.CreatedAt = DateTime.UtcNow;
        expectation.UpdatedAt = DateTime.UtcNow;
        await _expectationsCollection.InsertOneAsync(expectation);
        return expectation;
    }

    public async Task<IEnumerable<MockExpectation>> GetExpectationsAsync(string? environment = null, bool includeDisabled = false)
    {
        var filter = Builders<MockExpectation>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter &= Builders<MockExpectation>.Filter.Eq(x => x.Environment, environment);
        }

        if (!includeDisabled)
        {
            filter &= Builders<MockExpectation>.Filter.Eq(x => x.Enabled, true);
        }

        return await _expectationsCollection.Find(filter)
            .SortByDescending(x => x.Priority).ThenBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<MockExpectation?> GetExpectationByIdAsync(string id)
    {
        return await _expectationsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<bool> UpdateExpectationAsync(string id, MockExpectation expectation)
    {
        expectation.UpdatedAt = DateTime.UtcNow;
        var result = await _expectationsCollection.ReplaceOneAsync(x => x.Id == id, expectation);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteExpectationAsync(string id)
    {
        var result = await _expectationsCollection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> TryConsumeOnceAsync(string id)
    {
        var filter = Builders<MockExpectation>.Filter.And(
            Builders<MockExpectation>.Filter.Eq(x => x.Id, id),
            Builders<MockExpectation>.Filter.Eq("times.unlimited", false),
            Builders<MockExpectation>.Filter.Gt("times.remaining", 0)
        );

        var update = Builders<MockExpectation>.Update
            .Inc("times.remaining", -1)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _expectationsCollection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task CreateRequestLogAsync(MockRequestLog requestLog)
    {
        requestLog.Timestamp = DateTime.UtcNow;
        await _logsCollection.InsertOneAsync(requestLog);
    }

    public async Task<IEnumerable<MockRequestLog>> GetRequestLogsAsync(string? environment = null, string? path = null, int limit = 100, bool? matched = null)
    {
        var filter = Builders<MockRequestLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter &= Builders<MockRequestLog>.Filter.Eq(x => x.Environment, environment);
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            filter &= Builders<MockRequestLog>.Filter.Eq(x => x.Path, path);
        }

        if (matched.HasValue)
        {
            filter &= Builders<MockRequestLog>.Filter.Eq(x => x.Matched, matched.Value);
        }

        var cappedLimit = Math.Clamp(limit, 1, 1000);
        return await _logsCollection.Find(filter)
            .SortByDescending(x => x.Timestamp)
            .Limit(cappedLimit)
            .ToListAsync();
    }

    public async Task<long> DeleteRequestLogsAsync(string? environment = null)
    {
        var filter = Builders<MockRequestLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter = Builders<MockRequestLog>.Filter.Eq(x => x.Environment, environment);
        }

        var result = await _logsCollection.DeleteManyAsync(filter);
        return result.DeletedCount;
    }

    public async IAsyncEnumerable<MockRequestLog> StreamRequestLogsAsync(
        string? environment,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var filter = Builders<MockRequestLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter = Builders<MockRequestLog>.Filter.Eq(x => x.Environment, environment);
        }

        using var cursor = await _logsCollection.Find(filter).ToCursorAsync(ct);
        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var log in cursor.Current)
            {
                yield return log;
            }
        }
    }

    public async Task<long> DeleteRequestLogsOlderThanAsync(DateTime cutoffUtc)
    {
        var filter = Builders<MockRequestLog>.Filter.Lt(x => x.Timestamp, cutoffUtc);
        var result = await _logsCollection.DeleteManyAsync(filter);
        return result.DeletedCount;
    }

    public async Task CreateCallbackLogAsync(MockCallbackLog callbackLog)
    {
        await _callbackLogsCollection.InsertOneAsync(callbackLog);
    }

    public async Task<IEnumerable<MockCallbackLog>> GetCallbackLogsAsync(string? environment = null, string? source = null, string? correlationId = null, int limit = 100)
    {
        var filter = Builders<MockCallbackLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter &= Builders<MockCallbackLog>.Filter.Eq(x => x.Environment, environment);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            filter &= Builders<MockCallbackLog>.Filter.Eq(x => x.Source, source);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            filter &= Builders<MockCallbackLog>.Filter.Eq(x => x.CorrelationId, correlationId);
        }

        var cappedLimit = Math.Clamp(limit, 1, 1000);
        return await _callbackLogsCollection.Find(filter)
            .SortByDescending(x => x.SentAt)
            .Limit(cappedLimit)
            .ToListAsync();
    }

    public async Task<long> DeleteCallbackLogsAsync(string? environment = null)
    {
        var filter = Builders<MockCallbackLog>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter = Builders<MockCallbackLog>.Filter.Eq(x => x.Environment, environment);
        }

        var result = await _callbackLogsCollection.DeleteManyAsync(filter);
        return result.DeletedCount;
    }

    public async Task<long> DeleteCallbackLogsOlderThanAsync(DateTime cutoffUtc)
    {
        var filter = Builders<MockCallbackLog>.Filter.Lt(x => x.SentAt, cutoffUtc);
        var result = await _callbackLogsCollection.DeleteManyAsync(filter);
        return result.DeletedCount;
    }
}
