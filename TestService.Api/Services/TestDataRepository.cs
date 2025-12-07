using MongoDB.Bson;
using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface ITestDataRepository
{
    Task<IEnumerable<TestData>> GetAllAsync();
    Task<TestData?> GetByIdAsync(string id);
    Task<IEnumerable<TestData>> GetByCategoryAsync(string category);
    Task<TestData> CreateAsync(TestData testData);
    Task<bool> UpdateAsync(string id, TestData testData);
    Task<bool> DeleteAsync(string id);
    Task<Dictionary<string, decimal>> GetAggregatedDataByCategoryAsync();
}

public class TestDataRepository : ITestDataRepository
{
    private readonly IMongoCollection<TestData> _collection;

    public TestDataRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<TestData>(settings.CollectionName);
    }

    public async Task<IEnumerable<TestData>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<TestData?> GetByIdAsync(string id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TestData>> GetByCategoryAsync(string category)
    {
        return await _collection.Find(x => x.Category == category).ToListAsync();
    }

    public async Task<TestData> CreateAsync(TestData testData)
    {
        testData.CreatedAt = DateTime.UtcNow;
        testData.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(testData);
        return testData;
    }

    public async Task<bool> UpdateAsync(string id, TestData testData)
    {
        testData.UpdatedAt = DateTime.UtcNow;
        var result = await _collection.ReplaceOneAsync(x => x.Id == id, testData);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<Dictionary<string, decimal>> GetAggregatedDataByCategoryAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$category" },
                { "total", new BsonDocument("$sum", "$value") }
            })
        };

        var result = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return result.ToDictionary(
            doc => doc["_id"].AsString,
            doc => doc["total"].ToDecimal()
        );
    }
}
