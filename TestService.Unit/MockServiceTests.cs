using System.Diagnostics;
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

    // ── Callback logs: pass-throughs ─────────────────────────────────────────

    [Test]
    public async Task GetCallbackLogsAsync_PassesFiltersToRepository()
    {
        var logs = new[] { new MockCallbackLog { CorrelationId = "42" } };
        _repository.GetCallbackLogsAsync("e", "praxis", "42", 5).Returns(logs);

        var result = await _service.GetCallbackLogsAsync("e", "praxis", "42", 5);

        Assert.That(result, Is.SameAs(logs));
    }

    [Test]
    public async Task DeleteCallbackLogsAsync_ReturnsRepositoryCount()
    {
        _repository.DeleteCallbackLogsAsync("e").Returns(3L);

        var deleted = await _service.DeleteCallbackLogsAsync("e");

        Assert.That(deleted, Is.EqualTo(3L));
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
        StubLogs(new[] { log, log }); // 2 logs

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
        StubLogs(new[] { log });

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
        StubLogs(new[] { log, log, log });

        var request = new MockVerificationRequest
        {
            Matcher = new MockRequestMatcher { PathMatchType = PathMatchType.Exact, Path = "/api" },
            MinCount = 2
        };

        var result = await _service.VerifyAsync(request);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task VerifyAsync_StreamsEveryLog_BeyondOneThousand()
    {
        StubLogs(Enumerable.Range(0, 1500).Select(_ => new MockRequestLog { Method = "GET", Path = "/" }));

        var result = await _service.VerifyAsync(new MockVerificationRequest
        {
            Environment = "e",
            Matcher = new MockRequestMatcher { Path = "/" },
            ExactCount = 1500
        });

        Assert.That(result.Success, Is.True);
        Assert.That(result.MatchedCount, Is.EqualTo(1500));
        _repository.Received(1).StreamRequestLogsAsync("e", Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetRequestLogsAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<bool?>());
    }

    [Test]
    public async Task VerifyAsync_BlankEnvironment_PassesBlankThroughToStream()
    {
        StubLogs(new[]
        {
            new MockRequestLog { Environment = "a", Method = "GET", Path = "/" },
            new MockRequestLog { Environment = "b", Method = "GET", Path = "/" }
        });

        var result = await _service.VerifyAsync(new MockVerificationRequest
        {
            Environment = "",
            Matcher = new MockRequestMatcher { Path = "/" },
            ExactCount = 2
        });

        Assert.That(result.Success, Is.True);
        _repository.Received(1).StreamRequestLogsAsync("", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task VerifyAsync_QueryMatcher_CountsLogWithMatchingQueryString()
    {
        StubLogs(new[]
        {
            new MockRequestLog { Method = "GET", Path = "/q", QueryString = "?a=1" },
            new MockRequestLog { Method = "GET", Path = "/q", QueryString = "?a=2" },
            new MockRequestLog { Method = "GET", Path = "/q", QueryString = string.Empty }
        });

        var result = await _service.VerifyAsync(new MockVerificationRequest
        {
            Matcher = new MockRequestMatcher { Path = "/q", Query = new Dictionary<string, string> { ["a"] = "1" } },
            ExactCount = 1
        });

        Assert.That(result.MatchedCount, Is.EqualTo(1));
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task VerifyAsync_HeaderMatcher_IsCaseInsensitiveOnHeaderName()
    {
        StubLogs(new[]
        {
            new MockRequestLog { Method = "GET", Path = "/h", Headers = new Dictionary<string, string> { ["X-Test"] = "yes" } }
        });

        var result = await _service.VerifyAsync(new MockVerificationRequest
        {
            Matcher = new MockRequestMatcher { Path = "/h", Headers = new Dictionary<string, string> { ["x-test"] = "yes" } },
            ExactCount = 1
        });

        Assert.That(result.MatchedCount, Is.EqualTo(1));
    }

    // ── Regex timeout ────────────────────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_CatastrophicPathRegex_TimesOutAsNoMatchAndLogsWarning()
    {
        // "^/" rather than the bare "^(a+)+$": a path always starts with "/", which would fail the
        // bare pattern at position 0 without backtracking; this way the nested quantifier is reached.
        var expectation = BuildExpectation(path: "^/(a+)+$", pathMatchType: PathMatchType.Regex);
        expectation.Name = "evil";
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { expectation });

        var stopwatch = Stopwatch.StartNew();
        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/" + new string('a', 40) + "!" });
        stopwatch.Stop();

        Assert.That(result.Matched, Is.False);
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        Assert.That(WarningMessages(), Has.Some.Contains($"expectation {expectation.Id} (evil)"));
    }

    [Test]
    public async Task VerifyAsync_CatastrophicBodyRegex_TimesOutAsNoMatchAndLogsWarning()
    {
        StubLogs(new[] { new MockRequestLog { Method = "POST", Path = "/", Body = new string('a', 40) + "!" } });

        var stopwatch = Stopwatch.StartNew();
        var result = await _service.VerifyAsync(new MockVerificationRequest
        {
            Environment = "env1",
            Matcher = new MockRequestMatcher { Path = "/", Body = "^(a+)+$", BodyMatchType = BodyMatchType.Regex },
            ExactCount = 0
        });
        stopwatch.Stop();

        Assert.That(result.MatchedCount, Is.EqualTo(0));
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        Assert.That(WarningMessages(), Has.Some.Contains("verification matcher (environment env1)"));
    }

    // ── Invalid regex at match time ──────────────────────────────────────────

    [Test]
    public async Task ExecuteAsync_InvalidStoredPathRegex_IsNoMatchAndTheNextExpectationAnswers()
    {
        // Stored before patterns were validated on save.
        var invalid = BuildExpectation(path: "/(", pathMatchType: PathMatchType.Regex, status: 418);
        invalid.Name = "legacy";
        var valid = BuildExpectation(path: "/(", pathMatchType: PathMatchType.Exact, status: 200, body: "ok");
        _repository.GetExpectationsAsync(Arg.Any<string?>(), Arg.Any<bool>())
            .Returns(new[] { invalid, valid });

        var result = await _service.ExecuteAsync(new MockExecutionRequest { Path = "/(" });

        Assert.Multiple(() =>
        {
            Assert.That(result.Matched, Is.True);
            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.MatchedExpectationId, Is.EqualTo(valid.Id));
            Assert.That(WarningMessages(), Has.Some.Contains($"expectation {invalid.Id} (legacy) is invalid"));
        });
    }

    [Test]
    public async Task VerifyAsync_InvalidBodyRegex_CountsNothingAndLogsWarning()
    {
        StubLogs(new[] { new MockRequestLog { Method = "POST", Path = "/", Body = "[a-" } });

        var result = await _service.VerifyAsync(new MockVerificationRequest
        {
            Environment = "env1",
            Matcher = new MockRequestMatcher { Path = "/", Body = "[a-", BodyMatchType = BodyMatchType.Regex },
            ExactCount = 0
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.MatchedCount, Is.EqualTo(0));
            Assert.That(WarningMessages(), Has.Some.Contains("verification matcher (environment env1)"));
        });
    }

    // ── Regex validation on save ─────────────────────────────────────────────

    [Test]
    public void CreateExpectationAsync_InvalidPathRegex_ThrowsNamingFieldAndDoesNotStore()
    {
        var ex = Assert.ThrowsAsync<MockExpectationValidationException>(() => _service.CreateExpectationAsync(new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher { Path = "(", PathMatchType = PathMatchType.Regex }
        }));

        Assert.That(ex!.Field, Is.EqualTo("requestMatcher.path"));
        Assert.That(ex.Message, Does.StartWith("requestMatcher.path is not a valid regular expression: "));
        _repository.DidNotReceive().CreateExpectationAsync(Arg.Any<MockExpectation>());
    }

    [Test]
    public void CreateExpectationAsync_InvalidBodyRegex_ThrowsNamingBodyField()
    {
        var ex = Assert.ThrowsAsync<MockExpectationValidationException>(() => _service.CreateExpectationAsync(new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher { Body = "[a-", BodyMatchType = BodyMatchType.Regex }
        }));

        Assert.That(ex!.Field, Is.EqualTo("requestMatcher.body"));
        _repository.DidNotReceive().CreateExpectationAsync(Arg.Any<MockExpectation>());
    }

    [Test]
    public void UpdateExpectationAsync_InvalidPathRegex_ThrowsAndDoesNotStore()
    {
        _repository.GetExpectationByIdAsync("abc").Returns(new MockExpectation { Id = "abc" });

        var ex = Assert.ThrowsAsync<MockExpectationValidationException>(() => _service.UpdateExpectationAsync("abc", new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher { Path = "(", PathMatchType = PathMatchType.Regex }
        }));

        Assert.That(ex!.Field, Is.EqualTo("requestMatcher.path"));
        _repository.DidNotReceive().UpdateExpectationAsync(Arg.Any<string>(), Arg.Any<MockExpectation>());
    }

    [Test]
    public async Task CreateExpectationAsync_ValidRegexes_AreStored()
    {
        _repository.CreateExpectationAsync(Arg.Any<MockExpectation>()).Returns(ci => ci.Arg<MockExpectation>());

        await _service.CreateExpectationAsync(new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher
            {
                Path = @"^/api/items/\d+$",
                PathMatchType = PathMatchType.Regex,
                Body = @"""id""\s*:",
                BodyMatchType = BodyMatchType.Regex
            }
        });

        await _repository.Received(1).CreateExpectationAsync(Arg.Any<MockExpectation>());
    }

    [Test]
    public async Task CreateExpectationAsync_InvalidPatternWithNonRegexMatchType_IsStored()
    {
        _repository.CreateExpectationAsync(Arg.Any<MockExpectation>()).Returns(ci => ci.Arg<MockExpectation>());

        await _service.CreateExpectationAsync(new MockExpectation
        {
            RequestMatcher = new MockRequestMatcher { Path = "(", PathMatchType = PathMatchType.Prefix, Body = "(", BodyMatchType = BodyMatchType.Contains }
        });

        await _repository.Received(1).CreateExpectationAsync(Arg.Any<MockExpectation>());
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

    private void StubLogs(IEnumerable<MockRequestLog> logs)
    {
        var list = logs.ToList();
        _repository.StreamRequestLogsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => ToAsync(list));
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }

    private List<string> WarningMessages() => _logger.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)c.GetArguments()[0]! == LogLevel.Warning)
        .Select(c => c.GetArguments()[2]?.ToString() ?? string.Empty)
        .ToList();

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
