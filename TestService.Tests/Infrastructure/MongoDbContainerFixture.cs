using Testcontainers.MongoDb;

namespace TestService.Tests;

/// <summary>
/// Top-level setup fixture: starts a single MongoDB container before any test
/// runs, exposes its connection string for IntegrationTestBase to inject into the
/// test API host, and disposes the container after the test run.
///
/// Lives in the root TestService.Tests namespace so the [SetUpFixture] applies
/// to every fixture in the assembly (NUnit scopes [SetUpFixture] to its namespace
/// and descendants).
/// </summary>
[SetUpFixture]
public class MongoDbContainerFixture
{
    // Program.cs reads its Mongo connection string from these env vars at
    // top-level startup, before WebApplicationFactory's ConfigureAppConfiguration
    // overrides are applied. Setting the env var from this fixture is the only
    // way to redirect the in-process API at the test container.
    private const string ConnectionStringEnvVar = "MongoDbSettings__ConnectionString";
    private const string DatabaseNameEnvVar = "MongoDbSettings__DatabaseName";
    private const string TestDatabaseName = "TestServiceDb";

    private static MongoDbContainer? _container;

    /// <summary>
    /// Connection string of the running test container. Throws if accessed before
    /// OneTimeSetUp has run (defensive guard against fixture-ordering bugs).
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            if (_container is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(MongoDbContainerFixture)} has not been initialized. " +
                    "Ensure the test runner picks up the [SetUpFixture] before any [TestFixture].");
            }
            return _container.GetConnectionString();
        }
    }

    [OneTimeSetUp]
    public async Task StartContainer()
    {
        // Pin the Mongo major version to match the dev/prod stack (mongo:7 in
        // infrastructure/docker-compose.dev.yml). Use the image-parameter ctor;
        // the parameterless ctor is obsolete in Testcontainers.MongoDb 4.11+.
        _container = new MongoDbBuilder("mongo:7").Build();

        await _container.StartAsync();

        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _container.GetConnectionString());
        Environment.SetEnvironmentVariable(DatabaseNameEnvVar, TestDatabaseName);
    }

    [OneTimeTearDown]
    public async Task StopContainer()
    {
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
        Environment.SetEnvironmentVariable(DatabaseNameEnvVar, null);

        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }
}
