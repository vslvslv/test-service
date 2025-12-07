using MongoDB.Bson;
using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IDynamicEntityRepository
{
    Task<IEnumerable<DynamicEntity>> GetAllAsync(string entityType, bool excludeConsumed = false, string? environment = null);
    Task<DynamicEntity?> GetByIdAsync(string entityType, string id, bool markAsConsumed = false);
    Task<IEnumerable<DynamicEntity>> GetByFieldValueAsync(string entityType, string fieldName, object value, bool excludeConsumed = false, string? environment = null);
    Task<DynamicEntity?> GetNextAvailableAsync(string entityType, string? environment = null);
    Task<DynamicEntity> CreateAsync(DynamicEntity entity);
    Task<bool> UpdateAsync(string entityType, string id, DynamicEntity entity);
    Task<bool> DeleteAsync(string entityType, string id);
    Task<bool> MarkAsConsumedAsync(string entityType, string id);
    Task<bool> ResetConsumedAsync(string entityType, string id);
    Task<int> ResetAllConsumedAsync(string entityType, string? environment = null);
}

public class DynamicEntityRepository : IDynamicEntityRepository
{
    private readonly IMongoDatabase _database;

    public DynamicEntityRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
    }

    private IMongoCollection<BsonDocument> GetCollection(string entityType)
    {
        return _database.GetCollection<BsonDocument>($"Dynamic_{entityType}");
    }

    public async Task<IEnumerable<DynamicEntity>> GetAllAsync(string entityType, bool excludeConsumed = false, string? environment = null)
    {
        var collection = GetCollection(entityType);
        var filterBuilder = Builders<BsonDocument>.Filter;
        
        var filters = new List<FilterDefinition<BsonDocument>>();
        
        if (excludeConsumed)
        {
            filters.Add(filterBuilder.Or(
                filterBuilder.Eq("isConsumed", false),
                filterBuilder.Exists("isConsumed", false)
            ));
        }
        
        if (!string.IsNullOrEmpty(environment))
        {
            filters.Add(filterBuilder.Eq("environment", environment));
        }
        
        var filter = filters.Any() ? filterBuilder.And(filters) : filterBuilder.Empty;
        
        var documents = await collection.Find(filter).ToListAsync();
        return documents.Select(DynamicEntity.FromBsonDocument);
    }

    public async Task<DynamicEntity?> GetByIdAsync(string entityType, string id, bool markAsConsumed = false)
    {
        try
        {
            var collection = GetCollection(entityType);
            var objectId = new ObjectId(id);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
            var document = await collection.Find(filter).FirstOrDefaultAsync();
            
            if (document != null && markAsConsumed)
            {
                await MarkAsConsumedAsync(entityType, id);
                document["isConsumed"] = true;
                document["updatedAt"] = DateTime.UtcNow;
            }
            
            return document != null ? DynamicEntity.FromBsonDocument(document) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task<IEnumerable<DynamicEntity>> GetByFieldValueAsync(string entityType, string fieldName, object value, bool excludeConsumed = false, string? environment = null)
    {
        var collection = GetCollection(entityType);
        var filterBuilder = Builders<BsonDocument>.Filter;
        
        var filters = new List<FilterDefinition<BsonDocument>>
        {
            filterBuilder.Eq(fieldName, BsonValue.Create(value))
        };
        
        if (excludeConsumed)
        {
            filters.Add(filterBuilder.Or(
                filterBuilder.Eq("isConsumed", false),
                filterBuilder.Exists("isConsumed", false)
            ));
        }
        
        if (!string.IsNullOrEmpty(environment))
        {
            filters.Add(filterBuilder.Eq("environment", environment));
        }
        
        var filter = filterBuilder.And(filters);
        
        var documents = await collection.Find(filter).ToListAsync();
        return documents.Select(DynamicEntity.FromBsonDocument);
    }

    public async Task<DynamicEntity?> GetNextAvailableAsync(string entityType, string? environment = null)
    {
        var collection = GetCollection(entityType);
        var filterBuilder = Builders<BsonDocument>.Filter;
        
        // Find first non-consumed entity
        var filters = new List<FilterDefinition<BsonDocument>>
        {
            filterBuilder.Or(
                filterBuilder.Eq("isConsumed", false),
                filterBuilder.Exists("isConsumed", false)
            )
        };
        
        if (!string.IsNullOrEmpty(environment))
        {
            filters.Add(filterBuilder.Eq("environment", environment));
        }
        
        var filter = filterBuilder.And(filters);
        
        var updateDefinition = Builders<BsonDocument>.Update
            .Set("isConsumed", true)
            .Set("updatedAt", DateTime.UtcNow);
        
        // Use FindOneAndUpdate for atomic operation (prevents race conditions)
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After
        };
        
        var document = await collection.FindOneAndUpdateAsync(filter, updateDefinition, options);
        
        return document != null ? DynamicEntity.FromBsonDocument(document) : null;
    }

    public async Task<DynamicEntity> CreateAsync(DynamicEntity entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.IsConsumed = false; // Always start as not consumed
        
        var collection = GetCollection(entity.EntityType);
        var document = entity.ToBsonDocument();
        await collection.InsertOneAsync(document);
        
        // Get the clean ObjectId string without prefix
        entity.Id = document["_id"].AsObjectId.ToString();
        return entity;
    }

    public async Task<bool> UpdateAsync(string entityType, string id, DynamicEntity entity)
    {
        try
        {
            entity.UpdatedAt = DateTime.UtcNow;
            entity.Id = id;
            
            var collection = GetCollection(entityType);
            var objectId = new ObjectId(id);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
            var document = entity.ToBsonDocument();
            
            var result = await collection.ReplaceOneAsync(filter, document);
            return result.ModifiedCount > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string entityType, string id)
    {
        try
        {
            var collection = GetCollection(entityType);
            var objectId = new ObjectId(id);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
            var result = await collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<bool> MarkAsConsumedAsync(string entityType, string id)
    {
        try
        {
            var collection = GetCollection(entityType);
            var objectId = new ObjectId(id);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
            var update = Builders<BsonDocument>.Update
                .Set("isConsumed", true)
                .Set("updatedAt", DateTime.UtcNow);
            
            var result = await collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<bool> ResetConsumedAsync(string entityType, string id)
    {
        try
        {
            var collection = GetCollection(entityType);
            var objectId = new ObjectId(id);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", objectId);
            var update = Builders<BsonDocument>.Update
                .Set("isConsumed", false)
                .Set("updatedAt", DateTime.UtcNow);
            
            var result = await collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public async Task<int> ResetAllConsumedAsync(string entityType, string? environment = null)
    {
        var collection = GetCollection(entityType);
        var filterBuilder = Builders<BsonDocument>.Filter;
        
        var filters = new List<FilterDefinition<BsonDocument>>
        {
            filterBuilder.Eq("isConsumed", true)
        };
        
        if (!string.IsNullOrEmpty(environment))
        {
            filters.Add(filterBuilder.Eq("environment", environment));
        }
        
        var filter = filterBuilder.And(filters);
        
        var update = Builders<BsonDocument>.Update
            .Set("isConsumed", false)
            .Set("updatedAt", DateTime.UtcNow);
        
        var result = await collection.UpdateManyAsync(filter, update);
        return (int)result.ModifiedCount;
    }
}
