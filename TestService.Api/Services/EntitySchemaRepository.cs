using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IEntitySchemaRepository
{
    Task<IEnumerable<EntitySchema>> GetAllSchemasAsync();
    Task<EntitySchema?> GetSchemaByNameAsync(string entityName);
    Task<EntitySchema> CreateSchemaAsync(EntitySchema schema);
    Task<bool> UpdateSchemaAsync(string entityName, EntitySchema schema);
    Task<bool> DeleteSchemaAsync(string entityName);
    Task<bool> SchemaExistsAsync(string entityName);
}

public class EntitySchemaRepository : IEntitySchemaRepository
{
    private readonly IMongoCollection<EntitySchema> _collection;

    public EntitySchemaRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<EntitySchema>("EntitySchemas");
        
        // Create unique index on entityName
        var indexKeys = Builders<EntitySchema>.IndexKeys.Ascending(x => x.EntityName);
        var indexOptions = new CreateIndexOptions { Unique = true };
        var indexModel = new CreateIndexModel<EntitySchema>(indexKeys, indexOptions);
        _collection.Indexes.CreateOneAsync(indexModel);
    }

    public async Task<IEnumerable<EntitySchema>> GetAllSchemasAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<EntitySchema?> GetSchemaByNameAsync(string entityName)
    {
        return await _collection.Find(x => x.EntityName == entityName).FirstOrDefaultAsync();
    }

    public async Task<EntitySchema> CreateSchemaAsync(EntitySchema schema)
    {
        schema.CreatedAt = DateTime.UtcNow;
        schema.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(schema);
        return schema;
    }

    public async Task<bool> UpdateSchemaAsync(string entityName, EntitySchema schema)
    {
        schema.UpdatedAt = DateTime.UtcNow;
        var result = await _collection.ReplaceOneAsync(x => x.EntityName == entityName, schema);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteSchemaAsync(string entityName)
    {
        var result = await _collection.DeleteOneAsync(x => x.EntityName == entityName);
        return result.DeletedCount > 0;
    }

    public async Task<bool> SchemaExistsAsync(string entityName)
    {
        var count = await _collection.CountDocumentsAsync(x => x.EntityName == entityName);
        return count > 0;
    }
}
