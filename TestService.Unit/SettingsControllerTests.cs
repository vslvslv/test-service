using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestService.Api.Controllers;

namespace TestService.Unit;

[TestFixture]
public class SettingsControllerTests
{
    private ISettingsRepository _repo = null!;
    private SettingsController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = Substitute.For<ISettingsRepository>();
        _sut = BuildSut();
    }

    // ── GetSettings ───────────────────────────────────────────────────────────

    [Test]
    public async Task GetSettings_ReturnsOk_WithSettingsFromRepository()
    {
        var settings = new AppSettings { DataRetention = new DataRetentionSettings { SchemaRetentionDays = 30 } };
        _repo.GetSettingsAsync().Returns(settings);

        var result = await _sut.GetSettings();

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(settings));
    }

    [Test]
    public async Task GetSettings_Returns500_WhenRepositoryThrows()
    {
        _repo.GetSettingsAsync().Returns(Task.FromException<AppSettings>(new Exception("db failure")));

        var result = await _sut.GetSettings();

        var status = result.Result as ObjectResult;
        Assert.That(status?.StatusCode, Is.EqualTo(500));
    }

    // ── UpdateSettings ────────────────────────────────────────────────────────

    [Test]
    public async Task UpdateSettings_SetsUpdatedBy_FromUserClaims()
    {
        var input = new AppSettings { DataRetention = new DataRetentionSettings() };
        _repo.UpdateSettingsAsync(Arg.Any<AppSettings>()).Returns(input);

        await _sut.UpdateSettings(input);

        await _repo.Received(1).UpdateSettingsAsync(Arg.Is<AppSettings>(s => s.UpdatedBy == "admin"));
    }

    [Test]
    public async Task UpdateSettings_ReturnsOk_WithUpdatedSettings()
    {
        var updated = new AppSettings { DataRetention = new DataRetentionSettings { AutoCleanupEnabled = true } };
        _repo.UpdateSettingsAsync(Arg.Any<AppSettings>()).Returns(updated);

        var result = await _sut.UpdateSettings(new AppSettings { DataRetention = new DataRetentionSettings() });

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        Assert.That(ok!.Value, Is.SameAs(updated));
    }

    // ── GetApiKeys ────────────────────────────────────────────────────────────

    [Test]
    public async Task GetApiKeys_ReturnsOk_WithKeysList()
    {
        var keys = new List<ApiKey>
        {
            new() { Id = "1", Name = "Key A", Key = "ts_abc" },
            new() { Id = "2", Name = "Key B", Key = "ts_xyz" }
        };
        _repo.GetApiKeysAsync().Returns(keys);

        var result = await _sut.GetApiKeys();

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var returnedKeys = (ok!.Value as IEnumerable<ApiKey>)?.ToList();
        Assert.That(returnedKeys, Has.Count.EqualTo(2));
    }

    // ── CreateApiKey ──────────────────────────────────────────────────────────

    [Test]
    public async Task CreateApiKey_ReturnsCreated_WhenRequestIsValid()
    {
        var saved = new ApiKey { Id = "new-id", Name = "My Key", Key = "ts_generated" };
        _repo.CreateApiKeyAsync(Arg.Any<ApiKey>()).Returns(saved);

        var result = await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "My Key", ExpirationDays = 30 });

        var created = result.Result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Value, Is.SameAs(saved));
    }

    [Test]
    public async Task CreateApiKey_ReturnsBadRequest_WhenNameIsEmpty()
    {
        var result = await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "", ExpirationDays = 30 });

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task CreateApiKey_ReturnsBadRequest_WhenNameIsWhitespace()
    {
        var result = await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "   ", ExpirationDays = 30 });

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task CreateApiKey_SetsCreatedBy_FromUserClaims()
    {
        _repo.CreateApiKeyAsync(Arg.Any<ApiKey>()).Returns(x => x.Arg<ApiKey>());

        await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "Key", ExpirationDays = 7 });

        await _repo.Received(1).CreateApiKeyAsync(Arg.Is<ApiKey>(k => k.CreatedBy == "admin"));
    }

    [Test]
    public async Task CreateApiKey_SetsExpiresAt_WhenExpirationDaysProvided()
    {
        _repo.CreateApiKeyAsync(Arg.Any<ApiKey>()).Returns(x => x.Arg<ApiKey>());
        var before = DateTime.UtcNow;

        await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "Key", ExpirationDays = 30 });

        await _repo.Received(1).CreateApiKeyAsync(
            Arg.Is<ApiKey>(k => k.ExpiresAt.HasValue &&
                                k.ExpiresAt.Value >= before.AddDays(30) &&
                                k.ExpiresAt.Value <= DateTime.UtcNow.AddDays(30).AddSeconds(5)));
    }

    [Test]
    public async Task CreateApiKey_SetsExpiresAt_Null_WhenExpirationDaysIsNull()
    {
        _repo.CreateApiKeyAsync(Arg.Any<ApiKey>()).Returns(x => x.Arg<ApiKey>());

        await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "Key", ExpirationDays = null });

        await _repo.Received(1).CreateApiKeyAsync(Arg.Is<ApiKey>(k => k.ExpiresAt == null));
    }

    [Test]
    public async Task CreateApiKey_GeneratesKey_WithTsPrefix()
    {
        ApiKey? captured = null;
        _repo.CreateApiKeyAsync(Arg.Do<ApiKey>(k => captured = k)).Returns(x => x.Arg<ApiKey>());

        await _sut.CreateApiKey(new CreateApiKeyRequest { Name = "Key", ExpirationDays = 7 });

        Assert.That(captured?.Key, Does.StartWith("ts_"));
    }

    // ── DeleteApiKey ──────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteApiKey_ReturnsNoContent_WhenKeyExists()
    {
        _repo.DeleteApiKeyAsync("abc123").Returns(true);

        var result = await _sut.DeleteApiKey("abc123");

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }

    [Test]
    public async Task DeleteApiKey_ReturnsNotFound_WhenKeyDoesNotExist()
    {
        _repo.DeleteApiKeyAsync("missing").Returns(false);

        var result = await _sut.DeleteApiKey("missing");

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SettingsController BuildSut(string username = "admin")
    {
        var claims = new[] { new Claim(ClaimTypes.Name, username) };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var controller = new SettingsController(_repo, NullLogger<SettingsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }
}
