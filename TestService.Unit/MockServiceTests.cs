using Microsoft.Extensions.Logging;

namespace TestService.Unit;

[TestFixture]
public class MockServiceTests
{
    private IMockRepository _repository = null!;
    private ILogger<MockService> _logger = null!;
    private MockService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IMockRepository>();
        _logger = Substitute.For<ILogger<MockService>>();
        _service = new MockService(_repository, _logger);
    }

    // ── ExecuteAsync: path matching ──────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_ExactPathMatch_ReturnsMatchedResult()
    {
        var expectation = BuildExpectation(path: "/api/test", pathMatchType: PathMatchType.Exact, status: 200, body: "ok");
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/api/test" });

        Assert.That(result.Matched, Is.True);
        Assert.That(result.StatusCode, Is.EqualTo(200));
        Assert.That(result.Body, Is.EqualTo("ok"));
    }

    [Test]
    public async Task ExecuteAsync_ExactPathMismatch_ReturnsUnmatched()
    {
        var expectation = BuildExpectation(path: "/api/test", pathMatchType: PathMatchType.Exact);
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/api/other" });

        Assert.That(result.Matched, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task ExecuteAsync_PrefixPathMatch_ReturnsMatchedResult()
    {
        var expectation = BuildExpectation(path: "/api", pathMatchType: PathMatchType.Prefix, status: 201);
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/api/users/123" });

        Assert.That(result.Matched, Is.True);
        Assert.That(result.StatusCode, Is.EqualTo(201));
    }

    [Test]
    public async Task ExecuteAsync_RegexPathMatch_ReturnsMatchedResult()
    {
        var expectation = BuildExpectation(path: @"^/api/users/\d+$", pathMatchType: PathMatchType.Regex, status: 200);
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/api/users/42" });

        Assert.That(result.Matched, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_RegexPathNoMatch_ReturnsUnmatched()
    {
        var expectation = BuildExpectation(path: @"^/api/users/\d+$", pathMatchType: PathMatchType.Regex);
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/api/users/abc" });

        Assert.That(result.Matched, Is.False);
    }

    // ── ExecuteAsync: method matching ────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_MethodMatch_ReturnsMatchedResult()
    {
        var expectation = BuildExpectation(method: "POST");
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Method = "POST" });

        Assert.That(result.Matched, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_MethodMismatch_ReturnsUnmatched()
    {
        var expectation = BuildExpectation(method: "POST");
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Method = "GET" });

        Assert.That(result.Matched, Is.False);
    }

    // ── ExecuteAsync: header matching ────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_RequiredHeaderPresent_ReturnsMatchedResult()
    {
        var expectation = BuildExpectation();
        expectation.RequestMatcher.Headers["X-Api-Key"] = "secret";
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest
        {
            Headers = new Dictionary<string, string> { ["X-Api-Key"] = "secret" }
        });

        Assert.That(result.Matched, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_RequiredHeaderMissing_ReturnsUnmatched()
    {
        var expectation = BuildExpectation();
        expectation.RequestMatcher.Headers["X-Api-Key"] = "secret";
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Headers = new Dictionary<string, string>() });

        Assert.That(result.Matched, Is.False);
    }

    // ── ExecuteAsync: body matching ──────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_BodyMatchTypeAny_MatchesRegardlessOfBody()
    {
        var expectation = BuildExpectation();
        expectation.RequestMatcher.BodyMatchType = BodyMatchType.Any;
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Body = "anything" });

        Assert.That(result.Matched, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_BodyExactMatch_ReturnsMismatchOnDifferentBody()
    {
        var expectation = BuildExpectation();
        expectation.RequestMatcher.Body = "{\"id\":1}";
        expectation.RequestMatcher.BodyMatchType = BodyMatchType.Exact;
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Body = "{\"id\":2}" });

        Assert.That(result.Matched, Is.False);
    }

    [Test]
    public async Task ExecuteAsync_BodyContainsMatch_MatchesSubstring()
    {
        var expectation = BuildExpectation();
        expectation.RequestMatcher.Body = "hello";
        expectation.RequestMatcher.BodyMatchType = BodyMatchType.Contains;
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Body = "say hello world" });

        Assert.That(result.Matched, Is.True);
    }

    // ── ExecuteAsync: Times consumption ─────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_UnlimitedTimes_DoesNotCallTryConsume()
    {
        var expectation = BuildExpectation();
        expectation.Times = new MockTimes { Unlimited = true };
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        await _service.ExecuteAsync(new MockExecutionRequest());

        await _repository.DidNotReceive().TryConsumeOnceAsync(Arg.Any<string>());
    }

    [Test]
    public async Task ExecuteAsync_LimitedTimesExhausted_ReturnsUnmatched()
    {
        var expectation = BuildExpectation();
        expectation.Times = new MockTimes { Unlimited = false, Remaining = 0 };
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var result = await _service.ExecuteAsync(new MockExecutionRequest());

        Assert.That(result.Matched, Is.False);
    }

    [Test]
    public async Task ExecuteAsync_LimitedTimesAvailable_ConsumesAndMatches()
    {
        var expectation = BuildExpectation();
        expectation.Times = new MockTimes { Unlimited = false, Remaining = 3 };
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });
        _repository.TryConsumeOnceAsync(Arg.Any<string>()).Returns(true);

        var result = await _service.ExecuteAsync(new MockExecutionRequest());

        Assert.That(result.Matched, Is.True);
        await _repository.Received(1).TryConsumeOnceAsync(expectation.Id!);
    }

    // ── VerifyAsync ──────────────────────────────────────────────────────────

    [Test]
    public async Task VerifyAsync_ExactCount_SucceedsWhenCountMatches()
    {
        var log = new MockRequestLog { Method = "GET", Path = "/" };
        _repository.GetRequestLogsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<bool?>())
            .Returns(new[] { log, log }); // 2 logs

        var request = new MockVerificationRequest
        {
            Matcher = new MockRequestMatcher { PathMatchType = PathMatchType.Exact, Path = "/" },
            ExactCount = 2
        };

        var result = await _service.VerifyAsync(request);

        Assert.That(result.Success, Is.True);
        Assert.That(result.MatchedCount, Is.EqualTo(2));
    }

    [Test]
    public async Task VerifyAsync_ExactCount_FailsWhenCountDoesNotMatch()
    {
        var log = new MockRequestLog { Method = "GET", Path = "/" };
        _repository.GetRequestLogsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<bool?>())
            .Returns(new[] { log });

        var request = new MockVerificationRequest
        {
            Matcher = new MockRequestMatcher { PathMatchType = PathMatchType.Exact, Path = "/" },
            ExactCount = 3
        };

        var result = await _service.VerifyAsync(request);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task VerifyAsync_MinCount_SucceedsWhenAtLeastMinMatched()
    {
        var log = new MockRequestLog { Method = "GET", Path = "/api" };
        _repository.GetRequestLogsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<bool?>())
            .Returns(new[] { log, log, log });

        var request = new MockVerificationRequest
        {
            Matcher = new MockRequestMatcher { PathMatchType = PathMatchType.Exact, Path = "/api" },
            MinCount = 2
        };

        var result = await _service.VerifyAsync(request);

        Assert.That(result.Success, Is.True);
    }

    // ── CreateExpectationAsync: normalization ────────────────────────────────

    [Test]
    public async Task CreateExpectationAsync_NormalizesPathWithoutLeadingSlash()
    {
        MockExpectation? saved = null;
        _repository.CreateExpectationAsync(Arg.Do<MockExpectation>(e => saved = e))
            .Returns(Task.FromResult(new MockExpectation()));

        await _service.CreateExpectationAsync(new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher { Path = "api/users" }
        });

        Assert.That(saved!.RequestMatcher.Path, Is.EqualTo("/api/users"));
    }

    [Test]
    public async Task CreateExpectationAsync_NormalizesEmptyEnvironmentToDefault()
    {
        MockExpectation? saved = null;
        _repository.CreateExpectationAsync(Arg.Do<MockExpectation>(e => saved = e))
            .Returns(Task.FromResult(new MockExpectation()));

        await _service.CreateExpectationAsync(new MockExpectation { Environment = "  " });

        Assert.That(saved!.Environment, Is.EqualTo("default"));
    }

    [Test]
    public async Task CreateExpectationAsync_NormalizesMethodToUppercase()
    {
        MockExpectation? saved = null;
        _repository.CreateExpectationAsync(Arg.Do<MockExpectation>(e => saved = e))
            .Returns(Task.FromResult(new MockExpectation()));

        await _service.CreateExpectationAsync(new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher { Method = "post" }
        });

        Assert.That(saved!.RequestMatcher.Method, Is.EqualTo("POST"));
    }

    [Test]
    public async Task CreateExpectationAsync_NormalizesNegativeDelayToZero()
    {
        MockExpectation? saved = null;
        _repository.CreateExpectationAsync(Arg.Do<MockExpectation>(e => saved = e))
            .Returns(Task.FromResult(new MockExpectation()));

        await _service.CreateExpectationAsync(new MockExpectation
        {
            ResponseTemplate = new MockResponseTemplate { DelayMs = -100 }
        });

        Assert.That(saved!.ResponseTemplate.DelayMs, Is.EqualTo(0));
    }

    // ── UpdateExpectationAsync ───────────────────────────────────────────────

    [Test]
    public async Task UpdateExpectationAsync_ReturnsFalse_WhenExpectationDoesNotExist()
    {
        _repository.GetExpectationByIdAsync(Arg.Any<string>()).Returns((MockExpectation?)null);

        var result = await _service.UpdateExpectationAsync("nonexistent", new MockExpectation());

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task UpdateExpectationAsync_PreservesOriginalId_AndCreatedAt()
    {
        var originalCreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var existing = new MockExpectation { Id = "abc123", CreatedAt = originalCreatedAt };
        _repository.GetExpectationByIdAsync("abc123").Returns(existing);
        _repository.UpdateExpectationAsync(Arg.Any<string>(), Arg.Any<MockExpectation>()).Returns(true);

        MockExpectation? updated = null;
        _repository.UpdateExpectationAsync(Arg.Any<string>(), Arg.Do<MockExpectation>(e => updated = e))
            .Returns(true);

        await _service.UpdateExpectationAsync("abc123", new MockExpectation { Name = "new name" });

        Assert.That(updated!.Id, Is.EqualTo("abc123"));
        Assert.That(updated.CreatedAt, Is.EqualTo(originalCreatedAt));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MockExpectation BuildExpectation(
        string path = "/",
        PathMatchType pathMatchType = PathMatchType.Exact,
        string? method = null,
        int status = 200,
        string body = "")
    {
        return new MockExpectation
        {
            Id = "exp-" + Guid.NewGuid().ToString("N")[..8],
            RequestMatcher = new MockRequestMatcher
            {
                Path = path,
                PathMatchType = pathMatchType,
                Method = method
            },
            ResponseTemplate = new MockResponseTemplate { Status = status, Body = body },
            Times = new MockTimes { Unlimited = true }
        };
    }
}
