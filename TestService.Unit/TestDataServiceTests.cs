using Microsoft.Extensions.Logging;

namespace TestService.Unit;

[TestFixture]
public class TestDataServiceTests
{
    private ITestDataRepository _repository = null!;
    private IMessageBusService _messageBus = null!;
    private ILogger<TestDataService> _logger = null!;
    private TestDataService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ITestDataRepository>();
        _messageBus = Substitute.For<IMessageBusService>();
        _logger = Substitute.For<ILogger<TestDataService>>();
        _service = new TestDataService(_repository, _messageBus, _logger);
    }

    private static TestData NewTestData(string? id = null, string name = "Item", decimal value = 1m, string category = "cat") => new()
    {
        Id = id,
        Name = name,
        Value = value,
        Category = category
    };

    // ── GetAllAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task GetAllAsync_ReturnsAllItemsFromRepository()
    {
        var expected = new List<TestData>
        {
            NewTestData("1", "A", 1m, "cat"),
            NewTestData("2", "B", 2m, "cat")
        };
        _repository.GetAllAsync().Returns(expected);

        var result = await _service.GetAllAsync();

        Assert.That(result, Is.EqualTo(expected));
    }

    // ── GetByIdAsync ───────────────────────────────────────────────────────────

    [Test]
    public async Task GetByIdAsync_ReturnsItem_WhenExists()
    {
        var expected = NewTestData("abc", "Found", 5m, "cat");
        _repository.GetByIdAsync("abc").Returns(expected);

        var result = await _service.GetByIdAsync("abc");

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _repository.GetByIdAsync("missing").Returns((TestData?)null);

        var result = await _service.GetByIdAsync("missing");

        Assert.That(result, Is.Null);
    }

    // ── GetByCategoryAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task GetByCategoryAsync_ReturnsFilteredItems()
    {
        var expected = new List<TestData> { NewTestData("1", "A", 1m, "target") };
        _repository.GetByCategoryAsync("target").Returns(expected);

        var result = await _service.GetByCategoryAsync("target");

        Assert.That(result, Is.EqualTo(expected));
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateAsync_ReturnsCreatedItem_FromRepository()
    {
        var input = NewTestData(null, "New", 10m, "cat");
        var stored = NewTestData("new-id", input.Name, input.Value, input.Category);
        _repository.CreateAsync(input).Returns(stored);

        var result = await _service.CreateAsync(input);

        Assert.That(result.Id, Is.EqualTo("new-id"));
    }

    [Test]
    public async Task CreateAsync_PublishesCreatedEvent_WithStoredEntity()
    {
        var input = NewTestData(null, "New", 10m, "cat");
        var stored = NewTestData("new-id", input.Name, input.Value, input.Category);
        _repository.CreateAsync(input).Returns(stored);

        await _service.CreateAsync(input);

        await _messageBus.Received(1).PublishAsync(stored, "testdata.created");
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateAsync_ReturnsTrue_WhenRepositorySucceeds()
    {
        var testData = NewTestData(null, "Updated", 99m, "cat");
        _repository.UpdateAsync("existing", Arg.Any<TestData>()).Returns(true);

        var result = await _service.UpdateAsync("existing", testData);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task UpdateAsync_PublishesUpdatedEvent_WhenRepositorySucceeds()
    {
        var testData = NewTestData(null, "Updated", 99m, "cat");
        _repository.UpdateAsync("existing", Arg.Any<TestData>()).Returns(true);

        await _service.UpdateAsync("existing", testData);

        await _messageBus.Received(1).PublishAsync(Arg.Any<TestData>(), "testdata.updated");
    }

    [Test]
    public async Task UpdateAsync_ReturnsFalse_WhenRepositoryFails()
    {
        _repository.UpdateAsync("missing", Arg.Any<TestData>()).Returns(false);

        var result = await _service.UpdateAsync("missing", NewTestData());

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateAsync_DoesNotPublish_WhenRepositoryFails()
    {
        _repository.UpdateAsync("missing", Arg.Any<TestData>()).Returns(false);

        await _service.UpdateAsync("missing", NewTestData());

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<TestData>(), Arg.Any<string>());
    }

    [Test]
    public async Task UpdateAsync_AssignsIdToTestData_BeforePassingToRepository()
    {
        TestData? captured = null;
        _repository
            .UpdateAsync("target-id", Arg.Do<TestData>(td => captured = td))
            .Returns(true);

        await _service.UpdateAsync("target-id", NewTestData(null, "X"));

        Assert.That(captured?.Id, Is.EqualTo("target-id"));
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAsync_ReturnsTrue_WhenRepositorySucceeds()
    {
        _repository.DeleteAsync("del-id").Returns(true);

        var result = await _service.DeleteAsync("del-id");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task DeleteAsync_PublishesDeletedEvent_WhenRepositorySucceeds()
    {
        _repository.DeleteAsync("del-id").Returns(true);

        await _service.DeleteAsync("del-id");

        await _messageBus.Received(1).PublishAsync(Arg.Any<object>(), "testdata.deleted");
    }

    [Test]
    public async Task DeleteAsync_ReturnsFalse_WhenRepositoryFails()
    {
        _repository.DeleteAsync("missing").Returns(false);

        var result = await _service.DeleteAsync("missing");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task DeleteAsync_DoesNotPublish_WhenRepositoryFails()
    {
        _repository.DeleteAsync("missing").Returns(false);

        await _service.DeleteAsync("missing");

        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<string>());
    }

    // ── GetAggregatedDataByCategoryAsync ───────────────────────────────────────

    [Test]
    public async Task GetAggregatedDataByCategoryAsync_ReturnsRepositoryResult()
    {
        var expected = new Dictionary<string, decimal> { ["catA"] = 150m, ["catB"] = 75m };
        _repository.GetAggregatedDataByCategoryAsync().Returns(expected);

        var result = await _service.GetAggregatedDataByCategoryAsync();

        Assert.That(result, Is.EqualTo(expected));
    }
}
