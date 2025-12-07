using MongoDB.Driver;
using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IAgentRepository
{
    Task<IEnumerable<Agent>> GetAllAsync();
    Task<Agent?> GetByIdAsync(string id);
    Task<Agent?> GetByUsernameAsync(string username);
    Task<IEnumerable<Agent>> GetByBrandIdAsync(string brandId);
    Task<IEnumerable<Agent>> GetByLabelIdAsync(string labelId);
    Task<IEnumerable<Agent>> GetByOrientationTypeAsync(string orientationType);
    Task<IEnumerable<Agent>> GetByAgentTypeAsync(string agentType);
    Task<Agent> CreateAsync(Agent agent);
    Task<bool> UpdateAsync(string id, Agent agent);
    Task<bool> DeleteAsync(string id);
}

public class AgentRepository : IAgentRepository
{
    private readonly IMongoCollection<Agent> _collection;

    public AgentRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var database = client.GetDatabase(settings.DatabaseName);
        _collection = database.GetCollection<Agent>("Agents");
    }

    public async Task<IEnumerable<Agent>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    public async Task<Agent?> GetByIdAsync(string id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Agent?> GetByUsernameAsync(string username)
    {
        return await _collection.Find(x => x.Username == username).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Agent>> GetByBrandIdAsync(string brandId)
    {
        return await _collection.Find(x => x.BrandId == brandId).ToListAsync();
    }

    public async Task<IEnumerable<Agent>> GetByLabelIdAsync(string labelId)
    {
        return await _collection.Find(x => x.LabelId == labelId).ToListAsync();
    }

    public async Task<IEnumerable<Agent>> GetByOrientationTypeAsync(string orientationType)
    {
        return await _collection.Find(x => x.OrientationType == orientationType).ToListAsync();
    }

    public async Task<IEnumerable<Agent>> GetByAgentTypeAsync(string agentType)
    {
        return await _collection.Find(x => x.AgentType == agentType).ToListAsync();
    }

    public async Task<Agent> CreateAsync(Agent agent)
    {
        agent.CreatedAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(agent);
        return agent;
    }

    public async Task<bool> UpdateAsync(string id, Agent agent)
    {
        agent.UpdatedAt = DateTime.UtcNow;
        var result = await _collection.ReplaceOneAsync(x => x.Id == id, agent);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }
}
