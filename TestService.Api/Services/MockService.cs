using System.Text.RegularExpressions;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IMockService
{
    Task<MockExpectation> CreateExpectationAsync(MockExpectation expectation);
    Task<IEnumerable<MockExpectation>> GetExpectationsAsync(string? environment = null, bool includeDisabled = false);
    Task<MockExpectation?> GetExpectationByIdAsync(string id);
    Task<bool> UpdateExpectationAsync(string id, MockExpectation expectation);
    Task<bool> DeleteExpectationAsync(string id);

    Task<MockExecutionResult> ExecuteAsync(MockExecutionRequest request);

    Task<IEnumerable<MockRequestLog>> GetRequestLogsAsync(string? environment = null, string? path = null, int limit = 100, bool? matched = null);
    Task<long> DeleteRequestLogsAsync(string? environment = null);
    Task<MockVerificationResponse> VerifyAsync(MockVerificationRequest request);
}

public class MockService : IMockService
{
    private readonly IMockRepository _repository;
    private readonly ILogger<MockService> _logger;

    public MockService(IMockRepository repository, ILogger<MockService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<MockExpectation> CreateExpectationAsync(MockExpectation expectation)
    {
        NormalizeExpectation(expectation);
        return await _repository.CreateExpectationAsync(expectation);
    }

    public async Task<IEnumerable<MockExpectation>> GetExpectationsAsync(string? environment = null, bool includeDisabled = false)
    {
        return await _repository.GetExpectationsAsync(environment, includeDisabled);
    }

    public async Task<MockExpectation?> GetExpectationByIdAsync(string id)
    {
        return await _repository.GetExpectationByIdAsync(id);
    }

    public async Task<bool> UpdateExpectationAsync(string id, MockExpectation expectation)
    {
        var existing = await _repository.GetExpectationByIdAsync(id);
        if (existing == null)
        {
            return false;
        }

        NormalizeExpectation(expectation);
        expectation.Id = existing.Id;
        expectation.CreatedAt = existing.CreatedAt;

        return await _repository.UpdateExpectationAsync(id, expectation);
    }

    public async Task<bool> DeleteExpectationAsync(string id)
    {
        return await _repository.DeleteExpectationAsync(id);
    }

    public async Task<MockExecutionResult> ExecuteAsync(MockExecutionRequest request)
    {
        var expectations = (await _repository.GetExpectationsAsync(request.Environment, includeDisabled: false)).ToList();
        foreach (var expectation in expectations)
        {
            if (!Matches(expectation.RequestMatcher, request))
            {
                continue;
            }

            var consumed = await TryConsumeExpectationAsync(expectation);
            if (!consumed)
            {
                continue;
            }

            var result = new MockExecutionResult
            {
                Matched = true,
                StatusCode = expectation.ResponseTemplate.Status,
                Headers = new Dictionary<string, string>(expectation.ResponseTemplate.Headers, StringComparer.OrdinalIgnoreCase),
                Body = expectation.ResponseTemplate.Body ?? string.Empty,
                DelayMs = Math.Max(0, expectation.ResponseTemplate.DelayMs),
                MatchedExpectationId = expectation.Id,
                MatchedExpectationName = expectation.Name
            };

            await LogRequestAsync(request, result);
            return result;
        }

        var unmatched = new MockExecutionResult
        {
            Matched = false,
            StatusCode = StatusCodes.Status404NotFound,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Body = "{\"message\":\"No mock matched request\"}"
        };
        await LogRequestAsync(request, unmatched);
        return unmatched;
    }

    public async Task<IEnumerable<MockRequestLog>> GetRequestLogsAsync(string? environment = null, string? path = null, int limit = 100, bool? matched = null)
    {
        return await _repository.GetRequestLogsAsync(environment, path, limit, matched);
    }

    public async Task<long> DeleteRequestLogsAsync(string? environment = null)
    {
        return await _repository.DeleteRequestLogsAsync(environment);
    }

    public async Task<MockVerificationResponse> VerifyAsync(MockVerificationRequest request)
    {
        var logs = await _repository.GetRequestLogsAsync(request.Environment, limit: 1000);
        var matchedCount = logs.Count(x => Matches(request.Matcher, new MockExecutionRequest
        {
            Environment = x.Environment,
            Method = x.Method,
            Path = x.Path,
            QueryString = x.QueryString,
            Headers = x.Headers,
            Body = x.Body
        }));

        var success = true;
        if (request.ExactCount.HasValue)
        {
            success = matchedCount == request.ExactCount.Value;
        }
        else
        {
            if (request.MinCount.HasValue && matchedCount < request.MinCount.Value) success = false;
            if (request.MaxCount.HasValue && matchedCount > request.MaxCount.Value) success = false;
        }

        return new MockVerificationResponse
        {
            Success = success,
            MatchedCount = matchedCount,
            Message = success
                ? $"Verification succeeded. Matched {matchedCount} request(s)."
                : $"Verification failed. Matched {matchedCount} request(s)."
        };
    }

    private async Task<bool> TryConsumeExpectationAsync(MockExpectation expectation)
    {
        if (expectation.Times.Unlimited)
        {
            return true;
        }

        if (expectation.Times.Remaining <= 0 || string.IsNullOrWhiteSpace(expectation.Id))
        {
            return false;
        }

        return await _repository.TryConsumeOnceAsync(expectation.Id);
    }

    private async Task LogRequestAsync(MockExecutionRequest request, MockExecutionResult result)
    {
        await _repository.CreateRequestLogAsync(new MockRequestLog
        {
            Environment = request.Environment,
            Method = request.Method,
            Path = request.Path,
            QueryString = request.QueryString,
            Headers = request.Headers,
            Body = request.Body,
            Matched = result.Matched,
            MatchedExpectationId = result.MatchedExpectationId,
            MatchedExpectationName = result.MatchedExpectationName,
            ResponseStatusCode = result.StatusCode
        });
    }

    private static bool Matches(MockRequestMatcher matcher, MockExecutionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(matcher.Method))
        {
            if (!string.Equals(matcher.Method, request.Method, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!MatchPath(matcher, request.Path))
        {
            return false;
        }

        if (matcher.Query.Count > 0)
        {
            foreach (var kvp in matcher.Query)
            {
                if (!request.Query.TryGetValue(kvp.Key, out var actual) || !string.Equals(actual, kvp.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        if (matcher.Headers.Count > 0)
        {
            foreach (var kvp in matcher.Headers)
            {
                if (!request.Headers.TryGetValue(kvp.Key, out var actual) ||
                    !actual.Contains(kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (!MatchBody(matcher, request.Body))
        {
            return false;
        }

        return true;
    }

    private static bool MatchPath(MockRequestMatcher matcher, string requestPath)
    {
        var expected = string.IsNullOrWhiteSpace(matcher.Path) ? "/" : matcher.Path;
        return matcher.PathMatchType switch
        {
            PathMatchType.Exact => string.Equals(expected, requestPath, StringComparison.Ordinal),
            PathMatchType.Prefix => requestPath.StartsWith(expected, StringComparison.Ordinal),
            PathMatchType.Regex => Regex.IsMatch(requestPath, expected),
            _ => false
        };
    }

    private static bool MatchBody(MockRequestMatcher matcher, string? requestBody)
    {
        if (matcher.BodyMatchType == BodyMatchType.Any)
        {
            return true;
        }

        var expected = matcher.Body ?? string.Empty;
        var actual = requestBody ?? string.Empty;

        return matcher.BodyMatchType switch
        {
            BodyMatchType.Exact => string.Equals(actual, expected, StringComparison.Ordinal),
            BodyMatchType.Contains => actual.Contains(expected, StringComparison.Ordinal),
            BodyMatchType.Regex => Regex.IsMatch(actual, expected),
            _ => true
        };
    }

    private static void NormalizeExpectation(MockExpectation expectation)
    {
        expectation.Environment = string.IsNullOrWhiteSpace(expectation.Environment)
            ? "default"
            : expectation.Environment.Trim();
        expectation.Name = expectation.Name?.Trim() ?? string.Empty;

        expectation.RequestMatcher ??= new MockRequestMatcher();
        expectation.RequestMatcher.Method = expectation.RequestMatcher.Method?.Trim().ToUpperInvariant();
        expectation.RequestMatcher.Path = NormalizePath(expectation.RequestMatcher.Path);
        expectation.RequestMatcher.Query ??= new Dictionary<string, string>();
        expectation.RequestMatcher.Headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        expectation.ResponseTemplate ??= new MockResponseTemplate();
        expectation.ResponseTemplate.Headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        expectation.ResponseTemplate.DelayMs = Math.Max(0, expectation.ResponseTemplate.DelayMs);
        expectation.ResponseTemplate.Status = expectation.ResponseTemplate.Status <= 0 ? 200 : expectation.ResponseTemplate.Status;

        expectation.Times ??= new MockTimes();
        if (expectation.Times.Unlimited)
        {
            expectation.Times.Remaining = 0;
        }
        else
        {
            expectation.Times.Remaining = Math.Max(0, expectation.Times.Remaining);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }
        return normalized;
    }
}
