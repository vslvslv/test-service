using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TestService.Api.Models;
using TestService.Api.Services;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.Integration;

[TestFixture]
public class MocksControllerTests : IntegrationTestBase
{
    protected override async Task OnSetUp()
    {
        await SetAdminAuthTokenAsync();
    }

    [Test]
    public async Task MockRoute_WithMatchingExpectation_ReturnsConfiguredResponse()
    {
        var environment = $"test-{Guid.NewGuid():N}";
        var expectation = new MockExpectation
        {
            Environment = environment,
            Name = "hello route",
            RequestMatcher = new MockRequestMatcher
            {
                Method = "GET",
                Path = "/hello",
                PathMatchType = PathMatchType.Exact
            },
            ResponseTemplate = new MockResponseTemplate
            {
                Status = 201,
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                Body = "{\"ok\":true}"
            },
            Times = new MockTimes { Unlimited = true }
        };

        var createResponse = await Client.PostAsJsonAsync("/api/mocks/expectations", expectation);
        AssertStatusCode(createResponse, HttpStatusCode.Created);

        Client.DefaultRequestHeaders.Authorization = null;
        var response = await Client.GetAsync($"/mock/{environment}/hello");
        var body = await response.Content.ReadAsStringAsync();

        AssertStatusCode(response, HttpStatusCode.Created);
        Assert.That(body, Is.EqualTo("{\"ok\":true}"));
    }

    [Test]
    public async Task MockRoute_NoMatch_ReturnsNotFound()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        var response = await Client.GetAsync($"/mock/no-such-env-{Guid.NewGuid():N}/missing");

        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Verify_ReturnsSuccess_WhenRequestCountMatches()
    {
        var environment = $"verify-{Guid.NewGuid():N}";
        var expectation = new MockExpectation
        {
            Environment = environment,
            Name = "verify route",
            RequestMatcher = new MockRequestMatcher
            {
                Method = "GET",
                Path = "/ping",
                PathMatchType = PathMatchType.Exact
            },
            ResponseTemplate = new MockResponseTemplate
            {
                Status = 200,
                Body = "{\"pong\":true}"
            },
            Times = new MockTimes { Unlimited = true }
        };

        var createResponse = await Client.PostAsJsonAsync("/api/mocks/expectations", expectation);
        AssertStatusCode(createResponse, HttpStatusCode.Created);

        Client.DefaultRequestHeaders.Authorization = null;
        await Client.GetAsync($"/mock/{environment}/ping");
        await Client.GetAsync($"/mock/{environment}/ping");

        var token = await GetAdminTokenAsync();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var verifyRequest = new MockVerificationRequest
        {
            Environment = environment,
            Matcher = new MockRequestMatcher
            {
                Method = "GET",
                Path = "/ping",
                PathMatchType = PathMatchType.Exact
            },
            ExactCount = 2
        };

        var verifyResponse = await Client.PostAsJsonAsync("/api/mocks/verify", verifyRequest);
        AssertStatusCode(verifyResponse, HttpStatusCode.OK);
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<MockVerificationResponse>();
        Assert.That(verifyResult, Is.Not.Null);
        Assert.That(verifyResult!.Success, Is.True);
        Assert.That(verifyResult.MatchedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Verify_WithMoreThanOneThousandLogs_CountsEveryLog()
    {
        var environment = $"verify-many-{Guid.NewGuid():N}";
        var repository = Factory.Services.GetRequiredService<IMockRepository>();
        // Chunked so the driver's connection wait queue is not overrun.
        foreach (var chunk in Enumerable.Range(0, 1500).Chunk(50))
        {
            await Task.WhenAll(chunk.Select(_ => repository.CreateRequestLogAsync(new MockRequestLog
            {
                Environment = environment,
                Method = "GET",
                Path = "/many",
                ResponseStatusCode = 404
            })));
        }

        try
        {
            var response = await Client.PostAsJsonAsync("/api/mocks/verify", new MockVerificationRequest
            {
                Environment = environment,
                Matcher = new MockRequestMatcher { Method = "GET", Path = "/many" },
                ExactCount = 1500
            });

            AssertStatusCode(response, HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<MockVerificationResponse>();
            Assert.That(result!.Success, Is.True, result.Message);
            Assert.That(result.MatchedCount, Is.EqualTo(1500));
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task Verify_WithQueryMatcher_CountsRequestsWithThatQuery()
    {
        var environment = $"verify-query-{Guid.NewGuid():N}";
        try
        {
            Client.DefaultRequestHeaders.Authorization = null;
            await Client.GetAsync($"/mock/{environment}/search?a=1");
            await Client.GetAsync($"/mock/{environment}/search?a=1&b=2");
            await Client.GetAsync($"/mock/{environment}/search?a=2");
            await Client.GetAsync($"/mock/{environment}/search");
            await SetAdminAuthTokenAsync();

            var response = await Client.PostAsJsonAsync("/api/mocks/verify", new MockVerificationRequest
            {
                Environment = environment,
                Matcher = new MockRequestMatcher
                {
                    Method = "GET",
                    Path = "/search",
                    Query = new Dictionary<string, string> { ["a"] = "1" }
                },
                ExactCount = 2
            });

            AssertStatusCode(response, HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<MockVerificationResponse>();
            Assert.That(result!.MatchedCount, Is.EqualTo(2));
            Assert.That(result.Success, Is.True);
        }
        finally
        {
            await SetAdminAuthTokenAsync();
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task Verify_WithHeaderMatcher_IgnoresHeaderNameCase()
    {
        var environment = $"verify-header-{Guid.NewGuid():N}";
        try
        {
            await SendMockRequestAsync(environment, "/hdr", "GET", headers: new Dictionary<string, string> { ["X-Test"] = "on" });
            await SendMockRequestAsync(environment, "/hdr", "GET");

            var response = await Client.PostAsJsonAsync("/api/mocks/verify", new MockVerificationRequest
            {
                Environment = environment,
                Matcher = new MockRequestMatcher
                {
                    Method = "GET",
                    Path = "/hdr",
                    Headers = new Dictionary<string, string> { ["x-test"] = "on" }
                },
                ExactCount = 1
            });

            AssertStatusCode(response, HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<MockVerificationResponse>();
            Assert.That(result!.MatchedCount, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task Verify_SameRouteInTwoEnvironments_CountsOnlyTheRequestedEnvironment()
    {
        var environment = $"verify-env-a-{Guid.NewGuid():N}";
        var other = $"verify-env-b-{Guid.NewGuid():N}";
        try
        {
            await SendMockRequestAsync(environment, "/same", "GET");
            await SendMockRequestAsync(other, "/same", "GET");
            await SendMockRequestAsync(other, "/same", "GET");

            var response = await Client.PostAsJsonAsync("/api/mocks/verify", new MockVerificationRequest
            {
                Environment = environment,
                Matcher = new MockRequestMatcher { Method = "GET", Path = "/same" },
                ExactCount = 1
            });

            AssertStatusCode(response, HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<MockVerificationResponse>();
            Assert.That(result!.MatchedCount, Is.EqualTo(1));
            Assert.That(result.Success, Is.True);
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={other}");
        }
    }

    [Test]
    public async Task CreateExpectation_WithInvalidPathRegex_ReturnsBadRequestAndStoresNothing()
    {
        var environment = $"bad-regex-{Guid.NewGuid():N}";
        var response = await Client.PostAsJsonAsync("/api/mocks/expectations", new MockExpectation
        {
            Environment = environment,
            Name = "bad regex",
            RequestMatcher = new MockRequestMatcher { Path = "(", PathMatchType = PathMatchType.Regex }
        });

        AssertStatusCode(response, HttpStatusCode.BadRequest);
        Assert.That(await ReadMessageAsync(response), Does.Contain("requestMatcher.path"));
        Assert.That(await GetExpectationsInAsync(environment), Is.Empty);
    }

    [Test]
    public async Task CreateExpectation_WithInvalidBodyRegex_ReturnsBadRequestNamingBody()
    {
        var environment = $"bad-body-regex-{Guid.NewGuid():N}";
        var response = await Client.PostAsJsonAsync("/api/mocks/expectations", new MockExpectation
        {
            Environment = environment,
            Name = "bad body regex",
            RequestMatcher = new MockRequestMatcher { Path = "/x", Body = "[a-", BodyMatchType = BodyMatchType.Regex }
        });

        AssertStatusCode(response, HttpStatusCode.BadRequest);
        Assert.That(await ReadMessageAsync(response), Does.Contain("requestMatcher.body"));
        Assert.That(await GetExpectationsInAsync(environment), Is.Empty);
    }

    [Test]
    public async Task UpdateExpectation_WithInvalidPathRegex_ReturnsBadRequestAndKeepsStoredExpectation()
    {
        var environment = $"bad-regex-upd-{Guid.NewGuid():N}";
        var created = await CreateExpectationAsync(environment, "/valid", "valid");

        try
        {
            created.RequestMatcher.Path = "(";
            created.RequestMatcher.PathMatchType = PathMatchType.Regex;

            var response = await Client.PutAsJsonAsync($"/api/mocks/expectations/{created.Id}", created);

            AssertStatusCode(response, HttpStatusCode.BadRequest);
            Assert.That(await ReadMessageAsync(response), Does.Contain("requestMatcher.path"));
            var stored = (await GetExpectationsInAsync(environment)).Single();
            Assert.That(stored.RequestMatcher.Path, Is.EqualTo("/valid"));
            Assert.That(stored.RequestMatcher.PathMatchType, Is.EqualTo(PathMatchType.Exact));
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{created.Id}");
        }
    }

    [Test]
    public async Task DuplicateExpectation_WithInvalidPathRegex_ReturnsBadRequestAndStoresNothing()
    {
        // Seeded through the repository: the API no longer accepts an invalid pattern.
        var sourceEnvironment = $"bad-regex-src-{Guid.NewGuid():N}";
        var targetEnvironment = $"bad-regex-tgt-{Guid.NewGuid():N}";
        var repository = Factory.Services.GetRequiredService<IMockRepository>();
        var source = await repository.CreateExpectationAsync(new MockExpectation
        {
            Environment = sourceEnvironment,
            Name = "legacy bad regex",
            RequestMatcher = new MockRequestMatcher { Path = "(", PathMatchType = PathMatchType.Regex }
        });

        try
        {
            var response = await Client.PostAsJsonAsync(
                $"/api/mocks/expectations/{source.Id}/duplicate",
                new DuplicateExpectationRequest { TargetEnvironment = targetEnvironment });

            AssertStatusCode(response, HttpStatusCode.BadRequest);
            Assert.That(await ReadMessageAsync(response), Does.Contain("requestMatcher.path"));
            Assert.That(await GetExpectationsInAsync(targetEnvironment), Is.Empty);
        }
        finally
        {
            await repository.DeleteExpectationAsync(source.Id!);
        }
    }

    [Test]
    public async Task GetExpectations_WithEnvironmentFilter_ReturnsOnlyEnvironmentItems()
    {
        var envA = $"env-a-{Guid.NewGuid():N}";
        var envB = $"env-b-{Guid.NewGuid():N}";

        var createdA = await CreateExpectationAsync(envA, "/a", "env-a");
        var createdB = await CreateExpectationAsync(envB, "/b", "env-b");

        try
        {
            var response = await Client.GetAsync($"/api/mocks/expectations?environment={envA}");
            AssertStatusCode(response, HttpStatusCode.OK);

            var expectations = await response.Content.ReadFromJsonAsync<List<MockExpectation>>();
            Assert.That(expectations, Is.Not.Null);
            Assert.That(expectations!.Any(x => x.Id == createdA.Id), Is.True);
            Assert.That(expectations.Any(x => x.Id == createdB.Id), Is.False);
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{createdA.Id}");
            await Client.DeleteAsync($"/api/mocks/expectations/{createdB.Id}");
        }
    }

    [Test]
    public async Task UpdateExpectation_WithExistingId_UpdatesAndCanBeRetrieved()
    {
        var environment = $"upd-{Guid.NewGuid():N}";
        var created = await CreateExpectationAsync(environment, "/before", "before");

        try
        {
            created.Name = "after";
            created.Enabled = false;
            created.RequestMatcher.Path = "/after";

            var updateResponse = await Client.PutAsJsonAsync($"/api/mocks/expectations/{created.Id}", created);
            AssertStatusCode(updateResponse, HttpStatusCode.NoContent);

            var listResponse = await Client.GetAsync($"/api/mocks/expectations?environment={environment}&includeDisabled=true");
            AssertStatusCode(listResponse, HttpStatusCode.OK);

            var expectations = await listResponse.Content.ReadFromJsonAsync<List<MockExpectation>>();
            Assert.That(expectations, Is.Not.Null);

            var updated = expectations!.SingleOrDefault(x => x.Id == created.Id);
            Assert.That(updated, Is.Not.Null);
            Assert.That(updated!.Name, Is.EqualTo("after"));
            Assert.That(updated.Enabled, Is.False);
            Assert.That(updated.RequestMatcher.Path, Is.EqualTo("/after"));
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{created.Id}");
        }
    }

    [Test]
    public async Task DeleteExpectation_WithExistingId_RemovesExpectation()
    {
        var environment = $"del-{Guid.NewGuid():N}";
        var created = await CreateExpectationAsync(environment, "/to-delete", "delete me");

        var deleteResponse = await Client.DeleteAsync($"/api/mocks/expectations/{created.Id}");
        AssertStatusCode(deleteResponse, HttpStatusCode.NoContent);

        var listResponse = await Client.GetAsync($"/api/mocks/expectations?environment={environment}&includeDisabled=true");
        AssertStatusCode(listResponse, HttpStatusCode.OK);

        var expectations = await listResponse.Content.ReadFromJsonAsync<List<MockExpectation>>();
        Assert.That(expectations, Is.Not.Null);
        Assert.That(expectations!.Any(x => x.Id == created.Id), Is.False);
    }

    [Test]
    public async Task RequestLogs_FilterAndDeleteByEnvironment_Works()
    {
        var environment = $"logs-{Guid.NewGuid():N}";
        var created = await CreateExpectationAsync(environment, "/matched", "logs");

        try
        {
            Client.DefaultRequestHeaders.Authorization = null;
            await Client.GetAsync($"/mock/{environment}/matched");
            await Client.GetAsync($"/mock/{environment}/missing");

            var token = await GetAdminTokenAsync();
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var matchedResponse = await Client.GetAsync($"/api/mocks/requests?environment={environment}&matched=true&limit=50");
            AssertStatusCode(matchedResponse, HttpStatusCode.OK);
            var matchedLogs = await matchedResponse.Content.ReadFromJsonAsync<List<MockRequestLog>>();
            Assert.That(matchedLogs, Is.Not.Null);
            Assert.That(matchedLogs!.Count, Is.EqualTo(1));
            Assert.That(matchedLogs[0].Matched, Is.True);

            var deleteResponse = await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
            AssertStatusCode(deleteResponse, HttpStatusCode.OK);

            var verifyDeletedResponse = await Client.GetAsync($"/api/mocks/requests?environment={environment}&limit=50");
            AssertStatusCode(verifyDeletedResponse, HttpStatusCode.OK);
            var logsAfterDelete = await verifyDeletedResponse.Content.ReadFromJsonAsync<List<MockRequestLog>>();
            Assert.That(logsAfterDelete, Is.Not.Null);
            Assert.That(logsAfterDelete!, Is.Empty);
        }
        finally
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync());
            await Client.DeleteAsync($"/api/mocks/expectations/{created.Id}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task Verify_WithMinAndMaxCount_ReturnsExpectedResult()
    {
        var environment = $"range-{Guid.NewGuid():N}";
        var expectation = await CreateExpectationAsync(environment, "/range", "range verify");

        try
        {
            Client.DefaultRequestHeaders.Authorization = null;
            await Client.GetAsync($"/mock/{environment}/range");
            await Client.GetAsync($"/mock/{environment}/range");

            var token = await GetAdminTokenAsync();
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var successResponse = await Client.PostAsJsonAsync("/api/mocks/verify", new MockVerificationRequest
            {
                Environment = environment,
                Matcher = new MockRequestMatcher
                {
                    Method = "GET",
                    Path = "/range",
                    PathMatchType = PathMatchType.Exact
                },
                MinCount = 1,
                MaxCount = 3
            });
            AssertStatusCode(successResponse, HttpStatusCode.OK);
            var successPayload = await successResponse.Content.ReadFromJsonAsync<MockVerificationResponse>();
            Assert.That(successPayload, Is.Not.Null);
            Assert.That(successPayload!.Success, Is.True);

            var failureResponse = await Client.PostAsJsonAsync("/api/mocks/verify", new MockVerificationRequest
            {
                Environment = environment,
                Matcher = new MockRequestMatcher
                {
                    Method = "GET",
                    Path = "/range",
                    PathMatchType = PathMatchType.Exact
                },
                MinCount = 3
            });
            AssertStatusCode(failureResponse, HttpStatusCode.OK);
            var failurePayload = await failureResponse.Content.ReadFromJsonAsync<MockVerificationResponse>();
            Assert.That(failurePayload, Is.Not.Null);
            Assert.That(failurePayload!.Success, Is.False);
            Assert.That(failurePayload.MatchedCount, Is.EqualTo(2));
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{expectation.Id}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task GetExpectations_WithoutAuth_ReturnsUnauthorized()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/mocks/expectations");

        AssertStatusCode(response, HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetExpectations_ExcludeDisabledByDefault_AndIncludeWhenRequested()
    {
        var environment = $"disabled-{Guid.NewGuid():N}";
        var active = await CreateExpectationAsync(environment, "/active", "active");
        var disabled = await CreateExpectationAsync(environment, "/disabled", "disabled", enabled: false);

        try
        {
            var defaultResponse = await Client.GetAsync($"/api/mocks/expectations?environment={environment}");
            AssertStatusCode(defaultResponse, HttpStatusCode.OK);
            var defaultItems = await defaultResponse.Content.ReadFromJsonAsync<List<MockExpectation>>();
            Assert.That(defaultItems, Is.Not.Null);
            Assert.That(defaultItems!.Any(x => x.Id == active.Id), Is.True);
            Assert.That(defaultItems.Any(x => x.Id == disabled.Id), Is.False);

            var includeDisabledResponse = await Client.GetAsync($"/api/mocks/expectations?environment={environment}&includeDisabled=true");
            AssertStatusCode(includeDisabledResponse, HttpStatusCode.OK);
            var allItems = await includeDisabledResponse.Content.ReadFromJsonAsync<List<MockExpectation>>();
            Assert.That(allItems, Is.Not.Null);
            Assert.That(allItems!.Any(x => x.Id == active.Id), Is.True);
            Assert.That(allItems.Any(x => x.Id == disabled.Id), Is.True);
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{active.Id}");
            await Client.DeleteAsync($"/api/mocks/expectations/{disabled.Id}");
        }
    }

    [Test]
    public async Task PathMatch_PrefixAndRegex_WorkAsExpected()
    {
        var environment = $"path-{Guid.NewGuid():N}";
        var prefixExpectation = await CreateExpectationAsync(
            environment,
            "/api/prefix",
            "prefix",
            pathMatchType: PathMatchType.Prefix,
            body: "{\"type\":\"prefix\"}");

        var regexExpectation = await CreateExpectationAsync(
            environment,
            "/api/items/[0-9]+",
            "regex",
            pathMatchType: PathMatchType.Regex,
            body: "{\"type\":\"regex\"}");

        try
        {
            Client.DefaultRequestHeaders.Authorization = null;
            var prefixResponse = await Client.GetAsync($"/mock/{environment}/api/prefix/details");
            AssertStatusCode(prefixResponse, HttpStatusCode.OK);
            Assert.That(await prefixResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"type\":\"prefix\"}"));

            var regexResponse = await Client.GetAsync($"/mock/{environment}/api/items/123");
            AssertStatusCode(regexResponse, HttpStatusCode.OK);
            Assert.That(await regexResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"type\":\"regex\"}"));
        }
        finally
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync());
            await Client.DeleteAsync($"/api/mocks/expectations/{prefixExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/expectations/{regexExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task HeaderAndApplicationTypeRouting_ReturnsExpectedMockPerHeaderSet()
    {
        var environment = $"apptype-{Guid.NewGuid():N}";

        var webExpectation = await CreateExpectationAsync(
            environment,
            "/channel",
            "web-json",
            method: "POST",
            headers: new Dictionary<string, string>
            {
                ["X-Application-Type"] = "web",
                ["Content-Type"] = "application/json"
            },
            body: "{\"channel\":\"web\"}");

        var mobileExpectation = await CreateExpectationAsync(
            environment,
            "/channel",
            "mobile-xml",
            method: "POST",
            headers: new Dictionary<string, string>
            {
                ["X-Application-Type"] = "mobile",
                ["Content-Type"] = "application/xml"
            },
            body: "{\"channel\":\"mobile\"}");

        try
        {
            var webResponse = await SendMockRequestAsync(
                environment,
                "/channel",
                "POST",
                "{\"id\":1}",
                "application/json",
                new Dictionary<string, string> { ["X-Application-Type"] = "web" });

            AssertStatusCode(webResponse, HttpStatusCode.OK);
            Assert.That(await webResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"channel\":\"web\"}"));

            var mobileResponse = await SendMockRequestAsync(
                environment,
                "/channel",
                "POST",
                "<item id='1'/>",
                "application/xml",
                new Dictionary<string, string> { ["X-Application-Type"] = "mobile" });

            AssertStatusCode(mobileResponse, HttpStatusCode.OK);
            Assert.That(await mobileResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"channel\":\"mobile\"}"));
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{webExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/expectations/{mobileExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task BodyMatch_ExactContainsAndRegex_WorkAsExpected()
    {
        var environment = $"body-{Guid.NewGuid():N}";

        var exactExpectation = await CreateExpectationAsync(
            environment,
            "/body/exact",
            "exact",
            method: "POST",
            bodyMatchType: BodyMatchType.Exact,
            requestBody: "{\"type\":\"exact\"}",
            body: "{\"match\":\"exact\"}");

        var containsExpectation = await CreateExpectationAsync(
            environment,
            "/body/contains",
            "contains",
            method: "POST",
            bodyMatchType: BodyMatchType.Contains,
            requestBody: "\"orderId\":",
            body: "{\"match\":\"contains\"}");

        var regexExpectation = await CreateExpectationAsync(
            environment,
            "/body/regex",
            "regex",
            method: "POST",
            bodyMatchType: BodyMatchType.Regex,
            requestBody: "\"email\"\\s*:\\s*\"[^\"]+@example.com\"",
            body: "{\"match\":\"regex\"}");

        try
        {
            var exactResponse = await SendMockRequestAsync(environment, "/body/exact", "POST", "{\"type\":\"exact\"}");
            AssertStatusCode(exactResponse, HttpStatusCode.OK);
            Assert.That(await exactResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"match\":\"exact\"}"));

            var containsResponse = await SendMockRequestAsync(environment, "/body/contains", "POST", "{\"orderId\":123,\"name\":\"abc\"}");
            AssertStatusCode(containsResponse, HttpStatusCode.OK);
            Assert.That(await containsResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"match\":\"contains\"}"));

            var regexResponse = await SendMockRequestAsync(environment, "/body/regex", "POST", "{\"email\":\"user@example.com\"}");
            AssertStatusCode(regexResponse, HttpStatusCode.OK);
            Assert.That(await regexResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"match\":\"regex\"}"));
        }
        finally
        {
            await Client.DeleteAsync($"/api/mocks/expectations/{exactExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/expectations/{containsExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/expectations/{regexExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task LimitedTimes_ConsumesExpectationAndThenReturnsNotFound()
    {
        var environment = $"times-{Guid.NewGuid():N}";
        var limitedExpectation = await CreateExpectationAsync(
            environment,
            "/limited",
            "limited",
            body: "{\"ok\":true}",
            times: new MockTimes { Unlimited = false, Remaining = 1 });

        try
        {
            Client.DefaultRequestHeaders.Authorization = null;

            var first = await Client.GetAsync($"/mock/{environment}/limited");
            AssertStatusCode(first, HttpStatusCode.OK);

            var second = await Client.GetAsync($"/mock/{environment}/limited");
            AssertStatusCode(second, HttpStatusCode.NotFound);
        }
        finally
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync());
            await Client.DeleteAsync($"/api/mocks/expectations/{limitedExpectation.Id}");
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task DuplicateExpectation_WithValidId_ReturnsCloneInTargetEnvironment()
    {
        var sourceEnvironment = $"src-{Guid.NewGuid():N}";
        var targetEnvironment = $"tgt-{Guid.NewGuid():N}";
        var original = await CreateExpectationAsync(sourceEnvironment, "/duplicated", "duplicate source");

        var response = await Client.PostAsJsonAsync(
            $"/api/mocks/expectations/{original.Id}/duplicate",
            new DuplicateExpectationRequest { TargetEnvironment = targetEnvironment });

        AssertStatusCode(response, HttpStatusCode.OK);
        var clone = await response.Content.ReadFromJsonAsync<MockExpectation>();
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(clone.Id, Is.Not.EqualTo(original.Id));
        Assert.That(clone.Environment, Is.EqualTo(targetEnvironment));
        Assert.That(clone.RequestMatcher.Path, Is.EqualTo(original.RequestMatcher.Path));
    }

    [Test]
    public async Task DuplicateExpectation_WithUnknownId_ReturnsNotFound()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/mocks/expectations/507f1f77bcf86cd799439011/duplicate",
            new DuplicateExpectationRequest { TargetEnvironment = "anywhere" });

        AssertStatusCode(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DuplicateExpectation_WithEmptyTargetEnvironment_ReturnsBadRequest()
    {
        var sourceEnvironment = $"src-{Guid.NewGuid():N}";
        var original = await CreateExpectationAsync(sourceEnvironment, "/dup-bad", "duplicate bad target");

        var response = await Client.PostAsJsonAsync(
            $"/api/mocks/expectations/{original.Id}/duplicate",
            new DuplicateExpectationRequest { TargetEnvironment = "   " });

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ImportPostman_WithValidCollection_CreatesExpectations()
    {
        var environment = $"postman-{Guid.NewGuid():N}";
        var collectionJson = """
            {
              "info": {
                "name": "Sample",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
              },
              "item": [
                {
                  "name": "Get Foo",
                  "request": {
                    "method": "GET",
                    "url": {
                      "raw": "https://example.com/foo",
                      "host": ["example", "com"],
                      "path": ["foo"]
                    }
                  }
                }
              ]
            }
            """;

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(collectionJson));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "collection.json");

        var response = await Client.PostAsync(
            $"/api/mocks/expectations/import/postman?targetEnvironment={environment}",
            content);

        AssertStatusCode(response, HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PostmanImportResult>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Created, Is.GreaterThan(0));
    }

    [Test]
    public async Task ImportPostman_WithSavedResponse_MockAnswersWithThatResponse()
    {
        var environment = $"postman-resp-{Guid.NewGuid():N}";
        var collectionJson = """
            {
              "info": {
                "name": "Saved responses",
                "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
              },
              "item": [
                {
                  "name": "Create Order",
                  "request": {
                    "method": "POST",
                    "url": { "raw": "https://example.com/orders", "path": ["orders"] }
                  },
                  "response": [
                    {
                      "name": "created",
                      "code": 201,
                      "header": [ { "key": "Content-Type", "value": "application/json" } ],
                      "body": "{\"orderId\":42}"
                    }
                  ]
                }
              ]
            }
            """;

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(collectionJson));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "collection.json");

        var importResponse = await Client.PostAsync(
            $"/api/mocks/expectations/import/postman?targetEnvironment={environment}",
            content);
        AssertStatusCode(importResponse, HttpStatusCode.OK);
        var result = await importResponse.Content.ReadFromJsonAsync<PostmanImportResult>();
        Assert.That(result!.Created, Is.EqualTo(1));

        try
        {
            var mockResponse = await SendMockRequestAsync(environment, "/orders", "POST", "{}");

            AssertStatusCode(mockResponse, HttpStatusCode.Created);
            Assert.That(await mockResponse.Content.ReadAsStringAsync(), Is.EqualTo("{\"orderId\":42}"));
        }
        finally
        {
            foreach (var expectation in await GetExpectationsInAsync(environment))
            {
                await Client.DeleteAsync($"/api/mocks/expectations/{expectation.Id}");
            }
            await Client.DeleteAsync($"/api/mocks/requests?environment={environment}");
        }
    }

    [Test]
    public async Task ImportPostman_WithoutFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var response = await Client.PostAsync(
            $"/api/mocks/expectations/import/postman?targetEnvironment=any-{Guid.NewGuid():N}",
            content);

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ImportPostman_WithoutTargetEnvironment_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("{}"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", "collection.json");

        var response = await Client.PostAsync("/api/mocks/expectations/import/postman", content);

        AssertStatusCode(response, HttpStatusCode.BadRequest);
    }

    private async Task<List<MockExpectation>> GetExpectationsInAsync(string environment)
    {
        var response = await Client.GetAsync($"/api/mocks/expectations?environment={environment}&includeDisabled=true");
        AssertStatusCode(response, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<MockExpectation>>())!;
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    private async Task<MockExpectation> CreateExpectationAsync(
        string environment,
        string path,
        string name,
        bool enabled = true,
        string method = "GET",
        PathMatchType pathMatchType = PathMatchType.Exact,
        Dictionary<string, string>? headers = null,
        BodyMatchType bodyMatchType = BodyMatchType.Any,
        string? requestBody = null,
        string body = "{\"ok\":true}",
        MockTimes? times = null)
    {
        var expectation = new MockExpectation
        {
            Environment = environment,
            Name = name,
            Enabled = enabled,
            RequestMatcher = new MockRequestMatcher
            {
                Method = method,
                Path = path,
                PathMatchType = pathMatchType,
                Headers = headers ?? new Dictionary<string, string>(),
                BodyMatchType = bodyMatchType,
                Body = requestBody
            },
            ResponseTemplate = new MockResponseTemplate
            {
                Status = 200,
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                Body = body
            },
            Times = times ?? new MockTimes { Unlimited = true }
        };

        var response = await Client.PostAsJsonAsync("/api/mocks/expectations", expectation);
        AssertStatusCode(response, HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<MockExpectation>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null.And.Not.Empty);
        return created;
    }

    private async Task<HttpResponseMessage> SendMockRequestAsync(
        string environment,
        string path,
        string method,
        string? body = null,
        string contentType = "application/json",
        Dictionary<string, string>? headers = null)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/mock/{environment}{path}");

        if (body != null)
        {
            request.Content = new StringContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        var response = await Client.SendAsync(request);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminTokenAsync());
        return response;
    }
}
