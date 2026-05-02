using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestService.Unit;

[TestFixture]
public class TokenServiceTests
{
    private TokenService _tokenService = null!;
    private JwtSettings _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = new JwtSettings
        {
            SecretKey = "test-secret-key-that-is-long-enough-for-hmac256",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = 60
        };
        _tokenService = new TokenService(_settings, NullLogger<TokenService>.Instance);
    }

    private static User AdminUser() => new()
    {
        Id = "507f1f77bcf86cd799439011",
        Username = "alice",
        Email = "alice@example.com",
        Role = UserRole.Admin,
        CustomPermissions = []
    };

    // ── GenerateToken ──────────────────────────────────────────────────────────

    [Test]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = _tokenService.GenerateToken(AdminUser());

        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GenerateToken_TokenContainsThreeJwtSegments()
    {
        var token = _tokenService.GenerateToken(AdminUser());

        Assert.That(token.Split('.'), Has.Length.EqualTo(3),
            "A valid JWT must have exactly three dot-separated segments");
    }

    [Test]
    public void GenerateToken_IncludesUsernameClaim()
    {
        var user = AdminUser();
        var token = _tokenService.GenerateToken(user);

        var principal = _tokenService.ValidateToken(token);

        Assert.That(principal, Is.Not.Null);
        Assert.That(principal!.Identity?.Name, Is.EqualTo(user.Username));
    }

    [Test]
    public void GenerateToken_IncludesRoleClaim()
    {
        var user = AdminUser();
        var token = _tokenService.GenerateToken(user);

        var principal = _tokenService.ValidateToken(token);

        Assert.That(principal, Is.Not.Null);
        var role = principal!.FindFirstValue(ClaimTypes.Role);
        Assert.That(role, Is.EqualTo(UserRole.Admin.ToString()));
    }

    [Test]
    public void GenerateToken_IncludesPermissionClaims()
    {
        var user = AdminUser();
        var token = _tokenService.GenerateToken(user);

        var principal = _tokenService.ValidateToken(token);

        Assert.That(principal, Is.Not.Null);
        var permissions = principal!.FindAll("permission").Select(c => c.Value).ToList();
        Assert.That(permissions, Is.Not.Empty);
        Assert.That(permissions, Does.Contain(PermissionDefinitions.EntitiesRead));
    }

    // ── ValidateToken ──────────────────────────────────────────────────────────

    [Test]
    public void ValidateToken_ReturnsNull_ForGarbageInput()
    {
        var principal = _tokenService.ValidateToken("not.a.jwt");

        Assert.That(principal, Is.Null);
    }

    [Test]
    public void ValidateToken_ReturnsNull_ForTokenSignedWithDifferentKey()
    {
        var otherSettings = new JwtSettings
        {
            SecretKey = "completely-different-key-also-long-enough-hmac",
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            ExpirationMinutes = 60
        };
        var otherService = new TokenService(otherSettings, NullLogger<TokenService>.Instance);

        var token = otherService.GenerateToken(AdminUser());
        var principal = _tokenService.ValidateToken(token);

        Assert.That(principal, Is.Null);
    }

    [Test]
    public void ValidateToken_ReturnsNull_ForExpiredToken()
    {
        var expiredSettings = new JwtSettings
        {
            SecretKey = _settings.SecretKey,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            ExpirationMinutes = -1
        };
        var expiredService = new TokenService(expiredSettings, NullLogger<TokenService>.Instance);

        var token = expiredService.GenerateToken(AdminUser());
        var principal = _tokenService.ValidateToken(token);

        Assert.That(principal, Is.Null);
    }
}
