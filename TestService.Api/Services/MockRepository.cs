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
}

public class MockRepository : IMockRepository
{
    private readonly IMongoCollection<MockExpectation> _expectationsCollection;
    private readonly IMongoCollection<MockRequestLog> _logsCollection;

    public MockRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _expectationsCollection = database.GetCollection<MockExpectation>("MockExpectations");
        _logsCollection = database.GetCollection<MockRequestLog>("MockRequestLogs");

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
}
