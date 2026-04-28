using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration.Security;

[TestFixture]
public class SecurityAuthorizationMatrixTests : IntegrationTestBase
{
    private string _adminToken = null!;
    private string _contributorToken = null!;
    private string? _contributorUserId;

    protected override bool AuthenticateAsAdminByDefault => false;

    private static readonly (string Method, string Path, object? Body)[] ProtectedEndpointCases =
    [
        ("GET", "/api/users", null),
        ("GET", "/api/settings", null),
        ("GET", "/api/environments", null),
        ("GET", "/api/mocks/expectations", null),
        ("GET", "/api/testdata", null),
        ("POST", "/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "x",
            NewPassword = "y"
        })
    ];

    protected override async Task OnOneTimeSetUp()
    {
        _adminToken = await GetAdminTokenAsync();
        var createdUser = await CreateContributorUserAsync(_adminToken);
        _contributorUserId = createdUser.Id;
        _contributorToken = await GetTokenAsync(createdUser.Username, "Contrib@123");
    }

    protected override async Task OnOneTimeTearDown()
    {
        if (!string.IsNullOrWhiteSpace(_contributorUserId))
        {
            SetAuthToken(_adminToken);
            await Client.DeleteAsync($"/api/users/{_contributorUserId}");
        }
    }

    [Test]
    public async Task ProtectedEndpoints_WithoutAuth_ReturnUnauthorized()
    {
        foreach (var testCase in ProtectedEndpointCases)
        {
            Client.DefaultRequestHeaders.Authorization = null;
            var request = BuildRequest(testCase.Method, testCase.Path, testCase.Body);

            var response = await Client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                $"Expected 401 for {testCase.Method} {testCase.Path}, got {response.StatusCode}");
        }
    }

    [Test]
    public async Task ProtectedEndpoints_WithInvalidToken_ReturnUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        foreach (var testCase in ProtectedEndpointCases)
        {
            var request = BuildRequest(testCase.Method, testCase.Path, testCase.Body);
            var response = await Client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                $"Expected 401 for invalid token on {testCase.Method} {testCase.Path}, got {response.StatusCode}");
        }
    }

    [Test]
    public async Task Contributor_RestrictedEndpoints_ReturnForbidden()
    {
        SetAuthToken(_contributorToken);

        var requests = new[]
        {
            BuildRequest("GET", "/api/users"),
            BuildRequest("GET", "/api/settings"),
            BuildRequest("GET", "/api/settings/api-keys"),
            BuildRequest("GET", "/api/mocks/expectations"),
            BuildRequest("GET", "/api/mocks/requests"),
            BuildRequest("POST", "/api/mocks/expectations", new MockExpectation
            {
                Environment = "dev",
                Name = "blocked",
                RequestMatcher = new MockRequestMatcher { Method = "GET", Path = "/blocked" },
                ResponseTemplate = new MockResponseTemplate { Status = 200, Body = "{}" },
                Times = new MockTimes { Unlimited = true }
            }),
            BuildRequest("DELETE", "/api/mocks/requests?environment=dev")
        };

        foreach (var request in requests)
        {
            var response = await Client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden),
                $"Expected 403 for contributor on {request.Method} {request.RequestUri}");
        }
    }

    [Test]
    public async Task ChangePassword_WithWeakNewPassword_ReturnsBadRequest()
    {
        SetAuthToken(_contributorToken);

        var response = await Client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "Contrib@123",
            NewPassword = "weak"
        });

        AssertStatusCode(response, HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("at least 8 characters"));
    }

    [Test]
    public async Task CreateUser_WithWeakPassword_ReturnsBadRequest()
    {
        SetAuthToken(_adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await Client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = $"weak_{suffix}",
            Email = $"weak_{suffix}@test.local",
            Password = "weak",
            Role = UserRole.Contributor
        });

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task MocksExpectation_UpdateAndDelete_UnknownId_ReturnNotFound()
    {
        SetAuthToken(_adminToken);
        const string unknownId = "507f1f77bcf86cd799439011";

        var updateResponse = await Client.PutAsJsonAsync($"/api/mocks/expectations/{unknownId}", new MockExpectation
        {
            Environment = "dev",
            Name = "missing",
            RequestMatcher = new MockRequestMatcher { Method = "GET", Path = "/missing" },
            ResponseTemplate = new MockResponseTemplate { Status = 200, Body = "{}" },
            Times = new MockTimes { Unlimited = true }
        });
        AssertStatusCode(updateResponse, HttpStatusCode.NotFound);

        var deleteResponse = await Client.DeleteAsync($"/api/mocks/expectations/{unknownId}");
        AssertStatusCode(deleteResponse, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ActivitiesEndpoint_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/activities");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SchemasEndpoint_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/schemas");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static HttpRequestMessage BuildRequest(string method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<UserResponse> CreateContributorUserAsync(string adminToken)
    {
        SetAuthToken(adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"sec_{suffix}";
        var response = await Client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "Contrib@123",
            Role = UserRole.Contributor
        });

        AssertStatusCode(response, HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
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
}
