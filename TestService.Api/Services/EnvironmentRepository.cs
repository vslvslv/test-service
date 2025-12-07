using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IEnvironmentRepository
{
    Task<Models.Environment?> GetByIdAsync(string id);
    Task<Models.Environment?> GetByNameAsync(string name);
    Task<IEnumerable<Models.Environment>> GetAllAsync(bool includeInactive = false);
    Task<Models.Environment> CreateAsync(Models.Environment environment);
    Task<bool> UpdateAsync(string id, Models.Environment environment);
    Task<bool> DeleteAsync(string id);
    Task<bool> NameExistsAsync(string name);
}

public class EnvironmentRepository : IEnvironmentRepository
{
    private readonly IMongoCollection<Models.Environment> _collection;

    public EnvironmentRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<Models.Environment>("Environments");

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var nameIndexKeys = Builders<Models.Environment>.IndexKeys.Ascending(x => x.Name);
        var nameIndexOptions = new CreateIndexOptions { Unique = true };
        var nameIndexModel = new CreateIndexModel<Models.Environment>(nameIndexKeys, nameIndexOptions);

        var orderIndexKeys = Builders<Models.Environment>.IndexKeys.Ascending(x => x.Order);
        var orderIndexModel = new CreateIndexModel<Models.Environment>(orderIndexKeys);

        try
        {
            _collection.Indexes.CreateMany(new[] { nameIndexModel, orderIndexModel });
        }
        catch
        {
            // Indexes might already exist
        }
    }

    public async Task<Models.Environment?> GetByIdAsync(string id)
    {
        try
        {
            var filter = Builders<Models.Environment>.Filter.Eq(x => x.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch
        {
            return null;
        }
    }

    public async Task<Models.Environment?> GetByNameAsync(string name)
    {
        var filter = Builders<Models.Environment>.Filter.Eq(x => x.Name, name);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Models.Environment>> GetAllAsync(bool includeInactive = false)
    {
        var filterBuilder = Builders<Models.Environment>.Filter;
        var filter = includeInactive 
            ? filterBuilder.Empty 
            : filterBuilder.Eq(x => x.IsActive, true);

        var sort = Builders<Models.Environment>.Sort.Ascending(x => x.Order).Ascending(x => x.Name);
        return await _collection.Find(filter).Sort(sort).ToListAsync();
    }

    public async Task<Models.Environment> CreateAsync(Models.Environment environment)
    {
        environment.CreatedAt = DateTime.UtcNow;
        environment.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(environment);
        return environment;
    }

    public async Task<bool> UpdateAsync(string id, Models.Environment environment)
    {
        environment.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<Models.Environment>.Filter.Eq(x => x.Id, id);
        var result = await _collection.ReplaceOneAsync(filter, environment);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var filter = Builders<Models.Environment>.Filter.Eq(x => x.Id, id);
        var result = await _collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<bool> NameExistsAsync(string name)
    {
        var filter = Builders<Models.Environment>.Filter.Eq(x => x.Name, name);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }
}
