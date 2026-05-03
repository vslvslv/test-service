using Microsoft.Extensions.Logging.Abstractions;

namespace TestService.Unit;

[TestFixture]
public class UserServiceTests
{
    private IUserRepository _repo = null!;
    private IPasswordHasher _hasher = null!;
    private ITokenService _tokenService = null!;
    private UserService _sut = null!;

    private static readonly JwtSettings Settings = new()
    {
        SecretKey = "test-secret-key-long-enough-for-hmac256",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpirationMinutes = 60
    };

    [SetUp]
    public void SetUp()
    {
        _repo = Substitute.For<IUserRepository>();
        _hasher = Substitute.For<IPasswordHasher>();
        _tokenService = Substitute.For<ITokenService>();
        _sut = new UserService(_repo, _hasher, _tokenService,
            NullLogger<UserService>.Instance, Settings);
    }

    // ── LoginAsync ─────────────────────────────────────────────────────────────

    [Test]
    public async Task LoginAsync_ReturnsUserNotFoundOrInactive_WhenUserDoesNotExist()
    {
        _repo.GetByUsernameAsync("ghost").Returns((User?)null);

        var result = await _sut.LoginAsync(new LoginRequest { Username = "ghost", Password = "x" });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo(LoginFailureReason.UserNotFoundOrInactive));
    }

    [Test]
    public async Task LoginAsync_ReturnsUserNotFoundOrInactive_WhenUserIsInactive()
    {
        _repo.GetByUsernameAsync("alice").Returns(new User
        {
            Id = "1",
            Username = "alice",
            IsActive = false
        });

        var result = await _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "x" });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo(LoginFailureReason.UserNotFoundOrInactive));
    }

    [Test]
    public async Task LoginAsync_ReturnsInvalidPassword_WhenPasswordDoesNotMatch()
    {
        var user = new User { Id = "1", Username = "alice", IsActive = true };
        _repo.GetByUsernameAsync("alice").Returns(user);
        _hasher.VerifyPassword("wrong", user.PasswordHash).Returns(false);

        var result = await _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "wrong" });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo(LoginFailureReason.InvalidPassword));
    }

    [Test]
    public async Task LoginAsync_ReturnsSuccess_WithToken_WhenCredentialsAreValid()
    {
        var user = new User { Id = "1", Username = "alice", Email = "a@b.com", IsActive = true };
        _repo.GetByUsernameAsync("alice").Returns(user);
        _hasher.VerifyPassword("Password@1", user.PasswordHash).Returns(true);
        _tokenService.GenerateToken(user).Returns("jwt-token");

        var result = await _sut.LoginAsync(new LoginRequest { Username = "alice", Password = "Password@1" });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Response!.Token, Is.EqualTo("jwt-token"));
        Assert.That(result.Response.Username, Is.EqualTo("alice"));
    }

    // ── CreateAsync — password validation ─────────────────────────────────────

    [Test]
    public async Task CreateAsync_Throws_WhenUsernameAlreadyExists()
    {
        _repo.UsernameExistsAsync("alice").Returns(true);

        Assert.That(
            async () => await _sut.CreateAsync(new CreateUserRequest
            {
                Username = "alice",
                Email = "new@example.com",
                Password = "Password@1"
            }),
            Throws.InvalidOperationException.With.Message.Contains("alice"));
    }

    [Test]
    public async Task CreateAsync_Throws_WhenEmailAlreadyExists()
    {
        _repo.UsernameExistsAsync(Arg.Any<string>()).Returns(false);
        _repo.EmailExistsAsync("taken@example.com").Returns(true);

        Assert.That(
            async () => await _sut.CreateAsync(new CreateUserRequest
            {
                Username = "newuser",
                Email = "taken@example.com",
                Password = "Password@1"
            }),
            Throws.InvalidOperationException.With.Message.Contains("taken@example.com"));
    }

    [TestCase("short", "Password must be at least 8 characters")]
    [TestCase("nouppercase1@", "uppercase")]
    [TestCase("NOLOWERCASE1@", "lowercase")]
    [TestCase("NoDigitsHere@", "digit")]
    [TestCase("NoSpecial123", "special character")]
    public async Task CreateAsync_Throws_WhenPasswordViolatesStrengthRule(string password, string expectedFragment)
    {
        _repo.UsernameExistsAsync(Arg.Any<string>()).Returns(false);
        _repo.EmailExistsAsync(Arg.Any<string>()).Returns(false);

        Assert.That(
            async () => await _sut.CreateAsync(new CreateUserRequest
            {
                Username = "bob",
                Email = "bob@example.com",
                Password = password
            }),
            Throws.ArgumentException.With.Message.Contains(expectedFragment));
    }

    // ── DeleteAsync ────────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAsync_Throws_WhenDeletingLastActiveAdmin()
    {
        var lastAdmin = new User { Id = "1", Username = "admin", Role = UserRole.Admin, IsActive = true };
        _repo.GetByIdAsync("1").Returns(lastAdmin);
        _repo.GetAllAsync().Returns([lastAdmin]);

        Assert.That(
            async () => await _sut.DeleteAsync("1"),
            Throws.InvalidOperationException.With.Message.Contains("last admin"));
    }

    [Test]
    public async Task DeleteAsync_Succeeds_WhenAnotherAdminExists()
    {
        var admin1 = new User { Id = "1", Username = "admin1", Role = UserRole.Admin, IsActive = true };
        var admin2 = new User { Id = "2", Username = "admin2", Role = UserRole.Admin, IsActive = true };
        _repo.GetByIdAsync("1").Returns(admin1);
        _repo.GetAllAsync().Returns([admin1, admin2]);
        _repo.DeleteAsync("1").Returns(true);

        var result = await _sut.DeleteAsync("1");

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task DeleteAsync_ReturnsFalse_WhenUserNotFound()
    {
        _repo.GetByIdAsync("ghost").Returns((User?)null);

        var result = await _sut.DeleteAsync("ghost");

        Assert.That(result, Is.False);
    }
}
