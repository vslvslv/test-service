using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IAgentService
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

public class AgentService : IAgentService
{
    private readonly IAgentRepository _repository;
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IAgentRepository repository,
        IMessageBusService messageBus,
        ILogger<AgentService> logger)
    {
        _repository = repository;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<IEnumerable<Agent>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all agents");
        return await _repository.GetAllAsync();
    }

    public async Task<Agent?> GetByIdAsync(string id)
    {
        _logger.LogInformation("Retrieving agent with ID: {Id}", id);
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Agent?> GetByUsernameAsync(string username)
    {
        _logger.LogInformation("Retrieving agent with username: {Username}", username);
        return await _repository.GetByUsernameAsync(username);
    }

    public async Task<IEnumerable<Agent>> GetByBrandIdAsync(string brandId)
    {
        _logger.LogInformation("Retrieving agents for brand: {BrandId}", brandId);
        return await _repository.GetByBrandIdAsync(brandId);
    }

    public async Task<IEnumerable<Agent>> GetByLabelIdAsync(string labelId)
    {
        _logger.LogInformation("Retrieving agents for label: {LabelId}", labelId);
        return await _repository.GetByLabelIdAsync(labelId);
    }

    public async Task<IEnumerable<Agent>> GetByOrientationTypeAsync(string orientationType)
    {
        _logger.LogInformation("Retrieving agents with orientation type: {OrientationType}", orientationType);
        return await _repository.GetByOrientationTypeAsync(orientationType);
    }

    public async Task<IEnumerable<Agent>> GetByAgentTypeAsync(string agentType)
    {
        _logger.LogInformation("Retrieving agents with type: {AgentType}", agentType);
        return await _repository.GetByAgentTypeAsync(agentType);
    }

    public async Task<Agent> CreateAsync(Agent agent)
    {
        _logger.LogInformation("Creating new agent: {Username}", agent.Username);
        var created = await _repository.CreateAsync(agent);
        
        await _messageBus.PublishAsync(created, "agent.created");
        _logger.LogInformation("Published message for created agent: {Id}", created.Id);
        
        return created;
    }

    public async Task<bool> UpdateAsync(string id, Agent agent)
    {
        _logger.LogInformation("Updating agent with ID: {Id}", id);
        agent.Id = id;
        var result = await _repository.UpdateAsync(id, agent);
        
        if (result)
        {
            await _messageBus.PublishAsync(agent, "agent.updated");
            _logger.LogInformation("Published message for updated agent: {Id}", id);
        }
        
        return result;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("Deleting agent with ID: {Id}", id);
        var result = await _repository.DeleteAsync(id);
        
        if (result)
        {
            await _messageBus.PublishAsync(new { Id = id }, "agent.deleted");
            _logger.LogInformation("Published message for deleted agent: {Id}", id);
        }
        
        return result;
    }
}
