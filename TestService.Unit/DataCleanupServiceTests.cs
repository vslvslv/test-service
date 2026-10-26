using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestService.Api.BackgroundServices;

namespace TestService.Unit;

[TestFixture]
public class DataCleanupServiceTests
{
    private ISettingsRepository _settingsRepository = null!;
    private IEntitySchemaRepository _schemaRepository = null!;
    private IMockRepository _mockRepository = null!;
    private DataCleanupService _service = null!;
    private ServiceProvider _provider = null!;

    [SetUp]
    public void SetUp()
    {
        _settingsRepository = Substitute.For<ISettingsRepository>();
        _schemaRepository = Substitute.For<IEntitySchemaRepository>();
        _mockRepository = Substitute.For<IMockRepository>();

        var services = new ServiceCollection();
        services.AddSingleton(_settingsRepository);
        services.AddSingleton(_schemaRepository);
        services.AddSingleton(_mockRepository);
        // Parseable but never contacted: schema and entity retention stay null in every test.
        services.AddSingleton(new MongoDbSettings { ConnectionString = "mongodb://localhost:27017", DatabaseName = "unit" });
        _provider = services.BuildServiceProvider();

        _service = new DataCleanupService(_provider, NullLogger<DataCleanupService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        _provider.Dispose();
    }

    private void GivenSettings(int? mockLogRetentionDays, bool autoCleanupEnabled = true)
    {
        _settingsRepository.GetSettingsAsync().Returns(new AppSettings
        {
            DataRetention = new DataRetentionSettings
            {
                SchemaRetentionDays = null,
                EntityRetentionDays = null,
                MockLogRetentionDays = mockLogRetentionDays,
                AutoCleanupEnabled = autoCleanupEnabled
            }
        });
    }

    [Test]
    public async Task PerformCleanup_MockLogRetention7Days_DeletesRequestAndCallbackLogsOlderThan7Days()
    {
        GivenSettings(7);
        var expectedCutoff = DateTime.UtcNow.AddDays(-7);

        await _service.PerformCleanupAsync(CancellationToken.None);

        await _mockRepository.Received(1).DeleteRequestLogsOlderThanAsync(
            Arg.Is<DateTime>(d => Math.Abs((d - expectedCutoff).TotalSeconds) < 60));
        await _mockRepository.Received(1).DeleteCallbackLogsOlderThanAsync(
            Arg.Is<DateTime>(d => Math.Abs((d - expectedCutoff).TotalSeconds) < 60));
    }

    [Test]
    public async Task PerformCleanup_MockLogRetentionNull_DeletesNothing()
    {
        GivenSettings(null);

        await _service.PerformCleanupAsync(CancellationToken.None);

        await _mockRepository.DidNotReceive().DeleteRequestLogsOlderThanAsync(Arg.Any<DateTime>());
        await _mockRepository.DidNotReceive().DeleteCallbackLogsOlderThanAsync(Arg.Any<DateTime>());
    }

    [Test]
    public async Task PerformCleanup_AutoCleanupDisabled_DeletesNothing()
    {
        GivenSettings(7, autoCleanupEnabled: false);

        await _service.PerformCleanupAsync(CancellationToken.None);

        await _mockRepository.DidNotReceive().DeleteRequestLogsOlderThanAsync(Arg.Any<DateTime>());
        await _mockRepository.DidNotReceive().DeleteCallbackLogsOlderThanAsync(Arg.Any<DateTime>());
    }

    [Test]
    public async Task PerformCleanup_RequestLogDeleteFails_IsCaughtAndCallbackLogsAreStillDeleted()
    {
        GivenSettings(7);
        _mockRepository.DeleteRequestLogsOlderThanAsync(Arg.Any<DateTime>())
            .Returns(Task.FromException<long>(new InvalidOperationException("mongo down")));

        Assert.DoesNotThrowAsync(() => _service.PerformCleanupAsync(CancellationToken.None));
        await _mockRepository.Received(1).DeleteCallbackLogsOlderThanAsync(Arg.Any<DateTime>());
    }

    [Test]
    public void PerformCleanup_CallbackLogDeleteFails_IsCaught()
    {
        GivenSettings(7);
        _mockRepository.DeleteCallbackLogsOlderThanAsync(Arg.Any<DateTime>())
            .Returns(Task.FromException<long>(new InvalidOperationException("mongo down")));

        Assert.DoesNotThrowAsync(() => _service.PerformCleanupAsync(CancellationToken.None));
    }

    [TestCase("MockExpectations")]
    [TestCase("MockRequestLogs")]
    [TestCase("MockCallbackLogs")]
    [TestCase("activities")]
    [TestCase("Settings")]
    [TestCase("system.views")]
    [TestCase("PraxisMerchants")]
    [TestCase("PraxisTransactions")]
    [TestCase("EmulatorCounters")]
    [TestCase("TestData")]
    public void IsEntityCollection_MockEmulatorAndSystemCollections_AreExcluded(string name)
    {
        Assert.That(DataCleanupService.IsEntityCollection(name), Is.False);
    }

    [Test]
    public void IsEntityCollection_DynamicCollection_IsIncluded()
    {
        Assert.That(DataCleanupService.IsEntityCollection("Dynamic_x"), Is.True);
    }
}
