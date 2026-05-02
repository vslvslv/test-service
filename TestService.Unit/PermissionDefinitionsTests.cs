namespace TestService.Unit;

[TestFixture]
public class PermissionDefinitionsTests
{
    // ── GetRolePermissions ─────────────────────────────────────────────────────

    [Test]
    public void GetRolePermissions_Admin_ReturnsAllCatalogPermissions()
    {
        var catalogKeys = PermissionDefinitions.GetCatalog().Select(p => p.Key).ToHashSet();
        var adminPerms = PermissionDefinitions.GetRolePermissions(UserRole.Admin).ToHashSet();

        Assert.That(adminPerms, Is.EqualTo(catalogKeys));
    }

    [Test]
    public void GetRolePermissions_Contributor_IncludesEntityAndSchemaPermissions()
    {
        var perms = PermissionDefinitions.GetRolePermissions(UserRole.Contributor).ToList();

        Assert.That(perms, Does.Contain(PermissionDefinitions.EntitiesRead));
        Assert.That(perms, Does.Contain(PermissionDefinitions.EntitiesWrite));
        Assert.That(perms, Does.Contain(PermissionDefinitions.SchemasRead));
    }

    [Test]
    public void GetRolePermissions_Contributor_ExcludesAdminOnlyPermissions()
    {
        var perms = PermissionDefinitions.GetRolePermissions(UserRole.Contributor).ToList();

        Assert.That(perms, Does.Not.Contain(PermissionDefinitions.UsersCreate));
        Assert.That(perms, Does.Not.Contain(PermissionDefinitions.UsersDelete));
        Assert.That(perms, Does.Not.Contain(PermissionDefinitions.SettingsWrite));
    }

    // ── SanitizeCustomPermissions ──────────────────────────────────────────────

    [Test]
    public void SanitizeCustomPermissions_ReturnsEmpty_ForNull()
    {
        var result = PermissionDefinitions.SanitizeCustomPermissions(null);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void SanitizeCustomPermissions_FiltersOutInvalidKeys()
    {
        var result = PermissionDefinitions.SanitizeCustomPermissions(
            ["users.read", "totally.fake.permission", "schemas.read"]);

        Assert.That(result, Does.Not.Contain("totally.fake.permission"));
        Assert.That(result, Does.Contain(PermissionDefinitions.UsersRead));
        Assert.That(result, Does.Contain(PermissionDefinitions.SchemasRead));
    }

    [Test]
    public void SanitizeCustomPermissions_FiltersOutBlankEntries()
    {
        var result = PermissionDefinitions.SanitizeCustomPermissions(
            ["users.read", "", "   ", "schemas.read"]);

        Assert.That(result.Count(), Is.EqualTo(2));
    }

    [Test]
    public void SanitizeCustomPermissions_TrimsWhitespace()
    {
        var result = PermissionDefinitions.SanitizeCustomPermissions(["  users.read  "]);

        Assert.That(result, Does.Contain(PermissionDefinitions.UsersRead));
    }

    [Test]
    public void SanitizeCustomPermissions_Deduplicates()
    {
        var result = PermissionDefinitions.SanitizeCustomPermissions(
            ["users.read", "users.read", "users.read"]);

        Assert.That(result.Count(), Is.EqualTo(1));
    }

    [Test]
    public void SanitizeCustomPermissions_ReturnsSortedAlphabetically()
    {
        var result = PermissionDefinitions.SanitizeCustomPermissions(
            ["users.read", "entities.read", "schemas.read"]).ToList();

        Assert.That(result, Is.EqualTo(result.OrderBy(x => x).ToList()));
    }

    // ── GetEffectivePermissions ────────────────────────────────────────────────

    [Test]
    public void GetEffectivePermissions_AdminUser_ContainsAllCatalogPermissions()
    {
        var admin = new User { Role = UserRole.Admin };
        var catalogKeys = PermissionDefinitions.GetCatalog().Select(p => p.Key).ToHashSet();

        var effective = PermissionDefinitions.GetEffectivePermissions(admin).ToHashSet();

        Assert.That(effective, Is.SupersetOf(catalogKeys));
    }

    [Test]
    public void GetEffectivePermissions_ContributorWithValidCustomPermission_IncludesIt()
    {
        var user = new User
        {
            Role = UserRole.Contributor,
            CustomPermissions = [PermissionDefinitions.UsersRead]
        };

        var effective = PermissionDefinitions.GetEffectivePermissions(user);

        Assert.That(effective, Does.Contain(PermissionDefinitions.UsersRead));
    }

    [Test]
    public void GetEffectivePermissions_ContributorWithInvalidCustomPermission_ExcludesIt()
    {
        var user = new User
        {
            Role = UserRole.Contributor,
            CustomPermissions = ["not.a.real.permission"]
        };

        var effective = PermissionDefinitions.GetEffectivePermissions(user);

        Assert.That(effective, Does.Not.Contain("not.a.real.permission"));
    }
}
