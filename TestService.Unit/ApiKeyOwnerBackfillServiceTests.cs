using Microsoft.Extensions.Logging.Abstractions;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Unit;

[TestFixture]
public class ApiKeyOwnerBackfillServiceTests
{
    private ISettingsRepository _settings = null!;
    private IUserRepository _users = null!;
    private ApiKeyOwnerBackfillService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = Substitute.For<ISettingsRepository>();
        _users = Substitute.For<IUserRepository>();
        _sut = new ApiKeyOwnerBackfillService(
            _settings, _users, NullLogger<ApiKeyOwnerBackfillService>.Instance);
    }

    private static ApiKey Key(string id, string? createdBy, string? createdByUserId) => new()
    {
        Id = id,
        Name = $"key-{id}",
        Key = $"ts_{id}",
        CreatedBy = createdBy,
        CreatedByUserId = createdByUserId,
        IsActive = true
    };

    private static User UserWithId(string id, string username) =>
        new() { Id = id, Username = username, IsActive = true };

    [Test]
    public async Task BackfillsLegacyKey_FromCreatedByUsername()
    {
        _settings.GetApiKeysAsync().Returns(new[] { Key("k1", "admin", null) });
        _users.GetByUsernameAsync("admin").Returns(UserWithId("user-admin", "admin"));

        var count = await _sut.RunAsync();

        Assert.That(count, Is.EqualTo(1));
        await _settings.Received(1).UpdateApiKeyOwnerIdAsync("k1", "user-admin");
    }

    [Test]
    public async Task SkipsKeyThatAlreadyHasOwnerId()
    {
        _settings.GetApiKeysAsync().Returns(new[] { Key("k1", "admin", "user-admin") });

        var count = await _sut.RunAsync();

        Assert.That(count, Is.EqualTo(0));
        await _settings.DidNotReceive().UpdateApiKeyOwnerIdAsync(Arg.Any<string>(), Arg.Any<string>());
        await _users.DidNotReceive().GetByUsernameAsync(Arg.Any<string>());
    }

    [Test]
    public async Task SkipsKeyWithNoCreatedBy()
    {
        _settings.GetApiKeysAsync().Returns(new[] { Key("k1", null, null) });

        var count = await _sut.RunAsync();

        Assert.That(count, Is.EqualTo(0));
        await _settings.DidNotReceive().UpdateApiKeyOwnerIdAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task SkipsKeyWhenCreatorNoLongerExists()
    {
        // Fail closed: an orphaned key (creator deleted) must NOT be backfilled.
        _settings.GetApiKeysAsync().Returns(new[] { Key("k1", "ghost", null) });
        _users.GetByUsernameAsync("ghost").Returns((User?)null);

        var count = await _sut.RunAsync();

        Assert.That(count, Is.EqualTo(0));
        await _settings.DidNotReceive().UpdateApiKeyOwnerIdAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task ProcessesMultipleKeys_CountsOnlyBackfilled()
    {
        _settings.GetApiKeysAsync().Returns(new[]
        {
            Key("k1", "admin", null),           // eligible → backfilled
            Key("k2", "admin", "user-admin"),   // already has owner id → skipped
            Key("k3", "ghost", null)            // unknown creator → skipped
        });
        _users.GetByUsernameAsync("admin").Returns(UserWithId("user-admin", "admin"));
        _users.GetByUsernameAsync("ghost").Returns((User?)null);

        var count = await _sut.RunAsync();

        Assert.That(count, Is.EqualTo(1));
        await _settings.Received(1).UpdateApiKeyOwnerIdAsync("k1", "user-admin");
    }
}
