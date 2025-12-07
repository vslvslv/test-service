using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task<bool> UpdateAsync(string id, User user);
    Task<bool> DeleteAsync(string id);
    Task<bool> UsernameExistsAsync(string username);
    Task<bool> EmailExistsAsync(string email);
    Task UpdateLastLoginAsync(string id);
}

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _collection;

    public UserRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<User>("Users");

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var usernameIndexKeys = Builders<User>.IndexKeys.Ascending(x => x.Username);
        var usernameIndexOptions = new CreateIndexOptions { Unique = true };
        var usernameIndexModel = new CreateIndexModel<User>(usernameIndexKeys, usernameIndexOptions);

        var emailIndexKeys = Builders<User>.IndexKeys.Ascending(x => x.Email);
        var emailIndexOptions = new CreateIndexOptions { Unique = true };
        var emailIndexModel = new CreateIndexModel<User>(emailIndexKeys, emailIndexOptions);

        try
        {
            _collection.Indexes.CreateMany(new[] { usernameIndexModel, emailIndexModel });
        }
        catch
        {
            // Indexes might already exist
        }
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        try
        {
            var filter = Builders<User>.Filter.Eq(x => x.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch
        {
            return null;
        }
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Username, username);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Email, email);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(user);
        return user;
    }

    public async Task<bool> UpdateAsync(string id, User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<User>.Filter.Eq(x => x.Id, id);
        var result = await _collection.ReplaceOneAsync(filter, user);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Id, id);
        var result = await _collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Username, username);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Email, email);
        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task UpdateLastLoginAsync(string id)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Id, id);
        var update = Builders<User>.Update.Set(x => x.LastLoginAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update);
    }
}
