using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

[TestFixture]
public class UsersControllerTests : IntegrationTestBase
{
    private const string DefaultAdminUsername = "admin";

    [Test]
    public async Task GetUsers_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/users");

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetUsers_WithAdminAuth_ReturnsOk_AndIncludesAdminWithUsersReadPermission()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var response = await Client.GetAsync("/api/users");

        AssertStatusCode(response, HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.That(users, Is.Not.Null);
        Assert.That(users!, Is.Not.Empty);

        var adminUser = users.FirstOrDefault(u => u.Username == DefaultAdminUsername);
        Assert.That(adminUser, Is.Not.Null, "Expected admin user to exist in users list.");
        Assert.That(adminUser!.Permissions, Does.Contain(PermissionDefinitions.UsersRead));
    }

    [Test]
    public async Task AuthMe_WithAdminAuth_ReturnsUsersReadPermission()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var response = await Client.GetAsync("/api/auth/me");

        AssertStatusCode(response, HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(me, Is.Not.Null);
        Assert.That(me!.Username, Is.EqualTo(DefaultAdminUsername));
        Assert.That(me.Permissions, Does.Contain(PermissionDefinitions.UsersRead));
    }

    [Test]
    public async Task PermissionsCatalog_WithAdminAuth_ReturnsUsersReadAndRoleDefaults()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var response = await Client.GetAsync("/api/users/permissions/catalog");

        AssertStatusCode(response, HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        var payload = JsonNode.Parse(raw);
        Assert.That(payload, Is.Not.Null);

        var permissions = payload!["permissions"]?.AsArray();
        Assert.That(permissions, Is.Not.Null);
        Assert.That(
            permissions!.Any(p => p?["key"]?.GetValue<string>() == PermissionDefinitions.UsersRead),
            Is.True,
            "Permissions catalog should include users.read.");

        var adminDefaults = payload["roleDefaults"]?["Admin"]?.AsArray();
        Assert.That(adminDefaults, Is.Not.Null);
        Assert.That(adminDefaults!.Any(p => p?.GetValue<string>() == PermissionDefinitions.UsersRead), Is.True);

        var contributorDefaults = payload["roleDefaults"]?["Contributor"]?.AsArray();
        Assert.That(contributorDefaults, Is.Not.Null);
        Assert.That(contributorDefaults!.Any(p => p?.GetValue<string>() == PermissionDefinitions.UsersRead), Is.False);
    }

    [Test]
    public async Task GetUsers_WithContributorAuth_ReturnsForbidden()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"contrib_{suffix}";
        var email = $"{username}@test.local";
        var createRequest = new CreateUserRequest
        {
            Username = username,
            Email = email,
            Password = "Contrib@123",
            Role = UserRole.Contributor
        };

        string? createdUserId = null;

        try
        {
            var createResponse = await Client.PostAsJsonAsync("/api/users", createRequest);
            AssertStatusCode(createResponse, HttpStatusCode.Created);

            var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();
            Assert.That(createdUser, Is.Not.Null);
            createdUserId = createdUser!.Id;

            var contributorToken = await GetTokenAsync(username, createRequest.Password);
            SetAuthToken(contributorToken);

            var response = await Client.GetAsync("/api/users");

            AssertStatusCode(response, HttpStatusCode.Forbidden);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(createdUserId))
            {
                SetAuthToken(adminToken);
                await Client.DeleteAsync($"/api/users/{createdUserId}");
            }
        }
    }

    [Test]
    public async Task GetUserById_WithAdminAuth_ReturnsUser()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var createdUser = await CreateContributorUserAsync();
        try
        {
            var response = await Client.GetAsync($"/api/users/{createdUser.Id}");

            AssertStatusCode(response, HttpStatusCode.OK);
            var user = await response.Content.ReadFromJsonAsync<UserResponse>();
            Assert.That(user, Is.Not.Null);
            Assert.That(user!.Id, Is.EqualTo(createdUser.Id));
            Assert.That(user.Username, Is.EqualTo(createdUser.Username));
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, createdUser.Id);
        }
    }

    [Test]
    public async Task GetUserById_WithUnknownId_ReturnsNotFound()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var response = await Client.GetAsync("/api/users/507f1f77bcf86cd799439011");

        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetUserByUsername_WithAdminAuth_ReturnsUser()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var createdUser = await CreateContributorUserAsync();
        try
        {
            var response = await Client.GetAsync($"/api/users/username/{createdUser.Username}");

            AssertStatusCode(response, HttpStatusCode.OK);
            var user = await response.Content.ReadFromJsonAsync<UserResponse>();
            Assert.That(user, Is.Not.Null);
            Assert.That(user!.Username, Is.EqualTo(createdUser.Username));
            Assert.That(user.Id, Is.EqualTo(createdUser.Id));
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, createdUser.Id);
        }
    }

    [Test]
    public async Task UpdateUser_WithAdminAuth_UpdatesFields()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var createdUser = await CreateContributorUserAsync();
        try
        {
            var updateRequest = new UpdateUserRequest
            {
                FirstName = "Updated",
                LastName = "User",
                IsActive = false
            };

            var updateResponse = await Client.PutAsJsonAsync($"/api/users/{createdUser.Id}", updateRequest);
            AssertStatusCode(updateResponse, HttpStatusCode.NoContent);

            var getResponse = await Client.GetAsync($"/api/users/{createdUser.Id}");
            AssertStatusCode(getResponse, HttpStatusCode.OK);
            var updated = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.FirstName, Is.EqualTo("Updated"));
            Assert.That(updated.LastName, Is.EqualTo("User"));
            Assert.That(updated.IsActive, Is.False);
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, createdUser.Id);
        }
    }

    [Test]
    public async Task UpdateUser_WithUnknownId_ReturnsNotFound()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var response = await Client.PutAsJsonAsync("/api/users/507f1f77bcf86cd799439011", new UpdateUserRequest
        {
            FirstName = "Nobody"
        });

        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteUser_WithAdminAuth_RemovesUser()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var createdUser = await CreateContributorUserAsync();

        var deleteResponse = await Client.DeleteAsync($"/api/users/{createdUser.Id}");
        AssertStatusCode(deleteResponse, HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/users/{createdUser.Id}");
        AssertStatusCode(getResponse, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateUser_WithDuplicateUsername_ReturnsConflict()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var createdUser = await CreateContributorUserAsync();
        try
        {
            var duplicateRequest = new CreateUserRequest
            {
                Username = createdUser.Username,
                Email = $"dup_{Guid.NewGuid():N}@test.local",
                Password = "DupUser@123",
                Role = UserRole.Contributor
            };

            var response = await Client.PostAsJsonAsync("/api/users", duplicateRequest);
            AssertStatusCode(response, HttpStatusCode.Conflict);
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, createdUser.Id);
        }
    }

    [Test]
    public async Task CreateUser_WithDuplicateEmail_ReturnsConflict()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var createdUser = await CreateContributorUserAsync();
        try
        {
            var duplicateRequest = new CreateUserRequest
            {
                Username = $"dup_user_{Guid.NewGuid():N}"[..17],
                Email = createdUser.Email,
                Password = "DupEmail@123",
                Role = UserRole.Contributor
            };

            var response = await Client.PostAsJsonAsync("/api/users", duplicateRequest);
            AssertStatusCode(response, HttpStatusCode.Conflict);
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, createdUser.Id);
        }
    }

    [Test]
    public async Task UpdateUser_WithDuplicateEmail_ReturnsConflict()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var firstUser = await CreateContributorUserAsync();
        var secondUser = await CreateContributorUserAsync();
        try
        {
            var response = await Client.PutAsJsonAsync($"/api/users/{secondUser.Id}", new UpdateUserRequest
            {
                Email = firstUser.Email
            });

            AssertStatusCode(response, HttpStatusCode.Conflict);
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, firstUser.Id);
            await DeleteUserAsAdminAsync(adminToken, secondUser.Id);
        }
    }

    [Test]
    public async Task DeleteUser_WithUnknownId_ReturnsNotFound()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var response = await Client.DeleteAsync("/api/users/507f1f77bcf86cd799439011");

        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteUser_WhenDeletingLastActiveAdmin_ReturnsBadRequest()
    {
        var adminToken = await GetAdminTokenAsync();
        SetAuthToken(adminToken);

        var usersResponse = await Client.GetAsync("/api/users");
        AssertStatusCode(usersResponse, HttpStatusCode.OK);
        var users = await usersResponse.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.That(users, Is.Not.Null);

        var activeAdmins = users!
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .ToList();

        if (activeAdmins.Count != 1)
        {
            Assert.Inconclusive($"Expected exactly one active admin for deterministic test, found {activeAdmins.Count}.");
        }

        var response = await Client.DeleteAsync($"/api/users/{activeAdmins[0].Id}");
        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    private async Task<string> GetTokenAsync(string username, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        AssertStatusCode(response, HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.That(login, Is.Not.Null);
        Assert.That(login!.Token, Is.Not.Empty);
        return login.Token;
    }

    private void SetAuthToken(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<UserResponse> CreateContributorUserAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"user_{suffix}";
        var createRequest = new CreateUserRequest
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "User@1234",
            Role = UserRole.Contributor
        };

        var createResponse = await Client.PostAsJsonAsync("/api/users", createRequest);
        AssertStatusCode(createResponse, HttpStatusCode.Created);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(createdUser, Is.Not.Null);
        return createdUser!;
    }

    private async Task DeleteUserAsAdminAsync(string adminToken, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        SetAuthToken(adminToken);
        await Client.DeleteAsync($"/api/users/{userId}");
    }
}
