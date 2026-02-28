using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Auth;

[TestFixture]
public class AuthControllerTests : IntegrationTestBase
{
    [Test]
    public async Task ChangePassword_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "Anything@123",
            NewPassword = "NewPassword@123"
        });

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        var adminToken = await GetAdminTokenAsync();
        var testUser = await CreateContributorUserAsync(adminToken, "WrongCurrent@123");

        try
        {
            var userToken = await GetTokenAsync(testUser.Username, "WrongCurrent@123");
            SetAuthToken(userToken);

            var response = await Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
            {
                CurrentPassword = "Invalid@123",
                NewPassword = "NewValid@123"
            });

            AssertStatusCode(response, HttpStatusCode.BadRequest);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.That(payload, Does.Contain("Current password is incorrect"));
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, testUser.Id);
        }
    }

    [Test]
    public async Task ChangePassword_WithValidCurrentPassword_ChangesCredentials()
    {
        var adminToken = await GetAdminTokenAsync();
        var initialPassword = "Initial@123";
        var updatedPassword = "Updated@123";
        var testUser = await CreateContributorUserAsync(adminToken, initialPassword);

        try
        {
            var userToken = await GetTokenAsync(testUser.Username, initialPassword);
            SetAuthToken(userToken);

            var changeResponse = await Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
            {
                CurrentPassword = initialPassword,
                NewPassword = updatedPassword
            });
            AssertStatusCode(changeResponse, HttpStatusCode.NoContent);

            Client.DefaultRequestHeaders.Authorization = null;

            var oldLoginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = testUser.Username,
                Password = initialPassword
            });
            AssertStatusCode(oldLoginResponse, HttpStatusCode.Unauthorized);

            var newLoginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = testUser.Username,
                Password = updatedPassword
            });
            AssertStatusCode(newLoginResponse, HttpStatusCode.OK);
            var loginPayload = await newLoginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginPayload, Is.Not.Null);
            Assert.That(loginPayload!.Token, Is.Not.Empty);
        }
        finally
        {
            await DeleteUserAsAdminAsync(adminToken, testUser.Id);
        }
    }

    private async Task<UserResponse> CreateContributorUserAsync(string adminToken, string password)
    {
        SetAuthToken(adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"pwd_{suffix}";
        var request = new CreateUserRequest
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = password,
            Role = UserRole.Contributor
        };

        var createResponse = await Client.PostAsJsonAsync("/api/users", request);
        AssertStatusCode(createResponse, HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.That(created, Is.Not.Null);
        return created!;
    }

    private async Task<string> GetTokenAsync(string username, string password)
    {
        Client.DefaultRequestHeaders.Authorization = null;
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
