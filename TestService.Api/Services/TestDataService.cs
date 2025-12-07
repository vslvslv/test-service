using TestService.Api.Models;

namespace TestService.Api.Services;

public interface ITestDataService
{
    Task<IEnumerable<TestData>> GetAllAsync();
    Task<TestData?> GetByIdAsync(string id);
    Task<IEnumerable<TestData>> GetByCategoryAsync(string category);
    Task<TestData> CreateAsync(TestData testData);
    Task<bool> UpdateAsync(string id, TestData testData);
    Task<bool> DeleteAsync(string id);
    Task<Dictionary<string, decimal>> GetAggregatedDataByCategoryAsync();
}

public class TestDataService : ITestDataService
{
    private readonly ITestDataRepository _repository;
    private readonly IMessageBusService _messageBus;
    private readonly ILogger<TestDataService> _logger;

    public TestDataService(
        ITestDataRepository repository,
        IMessageBusService messageBus,
        ILogger<TestDataService> logger)
    {
        _repository = repository;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<IEnumerable<TestData>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all test data");
        return await _repository.GetAllAsync();
    }

    public async Task<TestData?> GetByIdAsync(string id)
    {
        _logger.LogInformation("Retrieving test data with ID: {Id}", id);
        return await _repository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<TestData>> GetByCategoryAsync(string category)
    {
        _logger.LogInformation("Retrieving test data for category: {Category}", category);
        return await _repository.GetByCategoryAsync(category);
    }

    public async Task<TestData> CreateAsync(TestData testData)
    {
        _logger.LogInformation("Creating new test data: {Name}", testData.Name);
        var created = await _repository.CreateAsync(testData);
        
        await _messageBus.PublishAsync(created, "testdata.created");
        _logger.LogInformation("Published message for created test data: {Id}", created.Id);
        
        return created;
    }

    public async Task<bool> UpdateAsync(string id, TestData testData)
    {
        _logger.LogInformation("Updating test data with ID: {Id}", id);
        testData.Id = id;
        var result = await _repository.UpdateAsync(id, testData);
        
        if (result)
        {
            await _messageBus.PublishAsync(testData, "testdata.updated");
            _logger.LogInformation("Published message for updated test data: {Id}", id);
        }
        
        return result;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        _logger.LogInformation("Deleting test data with ID: {Id}", id);
        var result = await _repository.DeleteAsync(id);
        
        if (result)
        {
            await _messageBus.PublishAsync(new { Id = id }, "testdata.deleted");
            _logger.LogInformation("Published message for deleted test data: {Id}", id);
        }
        
        return result;
    }

    public async Task<Dictionary<string, decimal>> GetAggregatedDataByCategoryAsync()
    {
        _logger.LogInformation("Aggregating test data by category");
        return await _repository.GetAggregatedDataByCategoryAsync();
    }
}
