using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TestService.Api.Emulators.Praxis;
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
            BuildRequest("DELETE", "/api/mocks/requests?environment=dev"),
            BuildRequest("GET", "/api/mocks/callbacks"),
            BuildRequest("DELETE", "/api/mocks/callbacks?environment=dev"),
            BuildRequest("GET", "/api/emulators/praxis/merchants"),
            BuildRequest("POST", "/api/emulators/praxis/merchants", MerchantRequest("dev")),
            BuildRequest("PUT", "/api/emulators/praxis/merchants/507f1f77bcf86cd799439011", MerchantRequest("dev")),
            BuildRequest("DELETE", "/api/emulators/praxis/merchants/507f1f77bcf86cd799439011"),
            BuildRequest("GET", "/api/emulators/praxis/transactions")
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

    [TestCase(PermissionDefinitions.MocksLogsRead, HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [TestCase(PermissionDefinitions.MocksLogsDelete, HttpStatusCode.Forbidden, HttpStatusCode.OK)]
    public async Task CallbackLogEndpoints_WithSinglePermission_EnforceTheirOwnPolicy(
        string permission, HttpStatusCode expectedGet, HttpStatusCode expectedDelete)
    {
        var user = await CreateContributorUserAsync(_adminToken, [permission]);
        try
        {
            SetAuthToken(await GetTokenAsync(user.Username, "Contrib@123"));
            var environment = $"cb-perm-{Guid.NewGuid():N}";

            var get = await Client.GetAsync($"/api/mocks/callbacks?environment={environment}");
            var delete = await Client.DeleteAsync($"/api/mocks/callbacks?environment={environment}");

            Assert.That(get.StatusCode, Is.EqualTo(expectedGet), $"GET with only {permission}");
            Assert.That(delete.StatusCode, Is.EqualTo(expectedDelete), $"DELETE with only {permission}");
        }
        finally
        {
            SetAuthToken(_adminToken);
            await Client.DeleteAsync($"/api/users/{user.Id}");
        }
    }

    [TestCase(PermissionDefinitions.MocksRead)]
    [TestCase(PermissionDefinitions.MocksWrite)]
    public async Task EmulatorEndpoints_WithSinglePermission_EnforceTheirOwnPolicy(string permission)
    {
        var user = await CreateContributorUserAsync(_adminToken, [permission]);
        var environment = $"px-perm-{Guid.NewGuid():N}";
        string? createdId = null;
        try
        {
            SetAuthToken(await GetTokenAsync(user.Username, "Contrib@123"));
            const string unknownId = "507f1f77bcf86cd799439011";
            var canRead = permission == PermissionDefinitions.MocksRead;

            var list = await Client.GetAsync($"/api/emulators/praxis/merchants?environment={environment}");
            var transactions = await Client.GetAsync($"/api/emulators/praxis/transactions?environment={environment}");
            var create = await Client.PostAsJsonAsync("/api/emulators/praxis/merchants", MerchantRequest(environment));
            var update = await Client.PutAsJsonAsync($"/api/emulators/praxis/merchants/{unknownId}", MerchantRequest(environment));
            var delete = await Client.DeleteAsync($"/api/emulators/praxis/merchants/{unknownId}");
            if (create.StatusCode == HttpStatusCode.Created)
            {
                createdId = (await create.Content.ReadFromJsonAsync<PraxisMerchantView>())!.Id;
            }

            Assert.Multiple(() =>
            {
                Assert.That(list.StatusCode, Is.EqualTo(canRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden), $"GET merchants with only {permission}");
                Assert.That(transactions.StatusCode, Is.EqualTo(canRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden), $"GET transactions with only {permission}");
                Assert.That(create.StatusCode, Is.EqualTo(canRead ? HttpStatusCode.Forbidden : HttpStatusCode.Created), $"POST merchants with only {permission}");
                Assert.That(update.StatusCode, Is.EqualTo(canRead ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound), $"PUT merchants with only {permission}");
                Assert.That(delete.StatusCode, Is.EqualTo(canRead ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound), $"DELETE merchants with only {permission}");
            });
        }
        finally
        {
            SetAuthToken(_adminToken);
            if (createdId is not null)
            {
                await Client.DeleteAsync($"/api/emulators/praxis/merchants/{createdId}");
            }
            await Client.DeleteAsync($"/api/users/{user.Id}");
        }
    }

    private static PraxisMerchantRequest MerchantRequest(string environment) => new()
    {
        Environment = environment,
        MerchantId = "m-perm",
        ApplicationKey = "app-perm",
        Secret = "secret-perm",
        Gateways = [new PraxisGateway { Id = "040e154f306f145b84208512d00ef8d9", Kind = "card" }]
    };

    private async Task<UserResponse> CreateContributorUserAsync(string adminToken, List<string>? customPermissions = null)
    {
        SetAuthToken(adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"sec_{suffix}";
        var response = await Client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "Contrib@123",
            Role = UserRole.Contributor,
            CustomPermissions = customPermissions
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
