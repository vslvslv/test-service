using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TestService.Api.Models;

namespace TestService.Tests;

[TestFixture]
public class TestDataApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetAll_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/testdata");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetAll_ReturnsJsonContent()
    {
        // Act
        var response = await _client.GetAsync("/api/testdata");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task Create_WithValidData_ReturnsCreated()
    {
        // Arrange
        var testData = new TestData
        {
            Name = "Test Item",
            Value = 100.50m,
            Category = "TestCategory",
            Metadata = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/testdata", testData);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        
        var createdData = await response.Content.ReadFromJsonAsync<TestData>();
        Assert.That(createdData, Is.Not.Null);
        Assert.That(createdData!.Name, Is.EqualTo(testData.Name));
        Assert.That(createdData.Value, Is.EqualTo(testData.Value));
        Assert.That(createdData.Category, Is.EqualTo(testData.Category));
        Assert.That(createdData.Id, Is.Not.Null);
    }

    [Test]
    public async Task GetById_WithExistingId_ReturnsTestData()
    {
        // Arrange - Create a test item first
        var testData = new TestData
        {
            Name = "Get By Id Test",
            Value = 50.25m,
            Category = "GetByIdCategory"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/testdata", testData);
        var createdData = await createResponse.Content.ReadFromJsonAsync<TestData>();

        // Act
        var response = await _client.GetAsync($"/api/testdata/{createdData!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrievedData = await response.Content.ReadFromJsonAsync<TestData>();
        Assert.That(retrievedData, Is.Not.Null);
        Assert.That(retrievedData!.Id, Is.EqualTo(createdData.Id));
        Assert.That(retrievedData.Name, Is.EqualTo(testData.Name));
    }

    [Test]
    public async Task GetById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = "507f1f77bcf86cd799439011";

        // Act
        var response = await _client.GetAsync($"/api/testdata/{nonExistingId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetByCategory_ReturnsFilteredResults()
    {
        // Arrange
        var category = "CategoryFilterTest";
        var testData1 = new TestData { Name = "Item 1", Value = 10, Category = category };
        var testData2 = new TestData { Name = "Item 2", Value = 20, Category = category };
        var testData3 = new TestData { Name = "Item 3", Value = 30, Category = "DifferentCategory" };

        await _client.PostAsJsonAsync("/api/testdata", testData1);
        await _client.PostAsJsonAsync("/api/testdata", testData2);
        await _client.PostAsJsonAsync("/api/testdata", testData3);

        // Act
        var response = await _client.GetAsync($"/api/testdata/category/{category}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var results = await response.Content.ReadFromJsonAsync<List<TestData>>();
        Assert.That(results, Is.Not.Null);
        Assert.That(results!.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(results.All(x => x.Category == category), Is.True);
    }

    [Test]
    public async Task Update_WithValidData_ReturnsNoContent()
    {
        // Arrange - Create a test item first
        var testData = new TestData
        {
            Name = "Original Name",
            Value = 100,
            Category = "UpdateCategory"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/testdata", testData);
        var createdData = await createResponse.Content.ReadFromJsonAsync<TestData>();

        // Modify the data
        createdData!.Name = "Updated Name";
        createdData.Value = 200;

        // Act
        var response = await _client.PutAsJsonAsync($"/api/testdata/{createdData.Id}", createdData);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify the update
        var getResponse = await _client.GetAsync($"/api/testdata/{createdData.Id}");
        var updatedData = await getResponse.Content.ReadFromJsonAsync<TestData>();
        Assert.That(updatedData!.Name, Is.EqualTo("Updated Name"));
        Assert.That(updatedData.Value, Is.EqualTo(200));
    }

    [Test]
    public async Task Delete_WithExistingId_ReturnsNoContent()
    {
        // Arrange - Create a test item first
        var testData = new TestData
        {
            Name = "To Be Deleted",
            Value = 100,
            Category = "DeleteCategory"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/testdata", testData);
        var createdData = await createResponse.Content.ReadFromJsonAsync<TestData>();

        // Act
        var response = await _client.DeleteAsync($"/api/testdata/{createdData!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var getResponse = await _client.GetAsync($"/api/testdata/{createdData.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetAggregatedData_ReturnsAggregatedResults()
    {
        // Arrange
        var category1 = "AggCategory1";
        var category2 = "AggCategory2";
        
        await _client.PostAsJsonAsync("/api/testdata", new TestData { Name = "Item 1", Value = 10, Category = category1 });
        await _client.PostAsJsonAsync("/api/testdata", new TestData { Name = "Item 2", Value = 20, Category = category1 });
        await _client.PostAsJsonAsync("/api/testdata", new TestData { Name = "Item 3", Value = 30, Category = category2 });

        // Act
        var response = await _client.GetAsync("/api/testdata/aggregated");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var results = await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>();
        Assert.That(results, Is.Not.Null);
        Assert.That(results!.Count, Is.GreaterThan(0));
    }
}
