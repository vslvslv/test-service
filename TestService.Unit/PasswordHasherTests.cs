namespace TestService.Unit;

[TestFixture]
public class PasswordHasherTests
{
    private PasswordHasher _hasher = null!;

    [SetUp]
    public void SetUp() => _hasher = new PasswordHasher();

    [Test]
    public void HashPassword_ProducesValidBase64()
    {
        var hash = _hasher.HashPassword("Password@1");

        Assert.That(() => Convert.FromBase64String(hash), Throws.Nothing);
    }

    [Test]
    public void HashPassword_ProducesDifferentHashEachCall()
    {
        var hash1 = _hasher.HashPassword("Password@1");
        var hash2 = _hasher.HashPassword("Password@1");

        Assert.That(hash1, Is.Not.EqualTo(hash2),
            "Each hash should embed a unique salt");
    }

    [Test]
    public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
    {
        var hash = _hasher.HashPassword("Password@1");

        Assert.That(_hasher.VerifyPassword("Password@1", hash), Is.True);
    }

    [Test]
    public void VerifyPassword_ReturnsFalse_ForWrongPassword()
    {
        var hash = _hasher.HashPassword("Password@1");

        Assert.That(_hasher.VerifyPassword("WrongPassword@2", hash), Is.False);
    }

    [Test]
    public void VerifyPassword_ReturnsFalse_ForEmptyPassword()
    {
        var hash = _hasher.HashPassword("Password@1");

        Assert.That(_hasher.VerifyPassword("", hash), Is.False);
    }

    [Test]
    public void VerifyPassword_ReturnsFalse_ForMalformedHash()
    {
        Assert.That(_hasher.VerifyPassword("Password@1", "not-a-valid-hash!!!"), Is.False);
    }

    [Test]
    public void VerifyPassword_ReturnsFalse_ForTamperedHash()
    {
        var hash = _hasher.HashPassword("Password@1");
        var tampered = hash[..^4] + "AAAA";

        Assert.That(_hasher.VerifyPassword("Password@1", tampered), Is.False);
    }
}
