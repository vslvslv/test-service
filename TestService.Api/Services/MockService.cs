using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
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
    Task<IEnumerable<MockCallbackLog>> GetCallbackLogsAsync(string? environment = null, string? source = null, string? correlationId = null, int limit = 100);
    Task<long> DeleteCallbackLogsAsync(string? environment = null);
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
        MockExpectationValidator.Validate(expectation);
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
        MockExpectationValidator.Validate(expectation);
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
            if (!Matches(expectation.RequestMatcher, request, $"expectation {expectation.Id} ({expectation.Name})"))
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

    public async Task<IEnumerable<MockCallbackLog>> GetCallbackLogsAsync(string? environment = null, string? source = null, string? correlationId = null, int limit = 100)
    {
        return await _repository.GetCallbackLogsAsync(environment, source, correlationId, limit);
    }

    public async Task<long> DeleteCallbackLogsAsync(string? environment = null)
    {
        return await _repository.DeleteCallbackLogsAsync(environment);
    }

    public async Task<MockVerificationResponse> VerifyAsync(MockVerificationRequest request)
    {
        var label = $"verification matcher (environment {request.Environment})";
        var matchedCount = 0;
        await foreach (var log in _repository.StreamRequestLogsAsync(request.Environment))
        {
            if (Matches(request.Matcher, ToExecutionRequest(log), label))
            {
                matchedCount++;
            }
        }

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

    /// <summary>
    /// Rebuilds a logged request in the shape <c>MockRoutingMiddleware</c> gives the live path:
    /// query parsed from the stored query string, both dictionaries case-insensitive.
    /// </summary>
    private static MockExecutionRequest ToExecutionRequest(MockRequestLog log)
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in QueryHelpers.ParseQuery(log.QueryString))
        {
            query.TryAdd(key, value.ToString());
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in log.Headers ?? new Dictionary<string, string>())
        {
            headers.TryAdd(key, value);
        }

        return new MockExecutionRequest
        {
            Environment = log.Environment,
            Method = log.Method,
            Path = log.Path,
            Query = query,
            QueryString = log.QueryString,
            Headers = headers,
            Body = log.Body
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

    private bool Matches(MockRequestMatcher matcher, MockExecutionRequest request, string label)
    {
        if (!string.IsNullOrWhiteSpace(matcher.Method))
        {
            if (!string.Equals(matcher.Method, request.Method, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!MatchPath(matcher, request.Path, label))
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

        if (!MatchBody(matcher, request.Body, label))
        {
            return false;
        }

        return true;
    }

    private bool MatchPath(MockRequestMatcher matcher, string requestPath, string label)
    {
        var expected = string.IsNullOrWhiteSpace(matcher.Path) ? "/" : matcher.Path;
        return matcher.PathMatchType switch
        {
            PathMatchType.Exact => string.Equals(expected, requestPath, StringComparison.Ordinal),
            PathMatchType.Prefix => requestPath.StartsWith(expected, StringComparison.Ordinal),
            PathMatchType.Regex => IsRegexMatch(requestPath, expected, label),
            _ => false
        };
    }

    private bool MatchBody(MockRequestMatcher matcher, string? requestBody, string label)
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
            BodyMatchType.Regex => IsRegexMatch(actual, expected, label),
            _ => true
        };
    }

    /// <summary>
    /// Regex match limited to <see cref="MockExpectationValidator.RegexTimeout"/>; a timeout counts as no match, and so does
    /// an invalid pattern (an expectation stored before patterns were validated, or a verification matcher).
    /// </summary>
    private bool IsRegexMatch(string input, string pattern, string label)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.None, MockExpectationValidator.RegexTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            _logger.LogWarning(
                "Regex {Pattern} of {Label} timed out after {TimeoutMs} ms; treated as no match",
                pattern,
                label,
                MockExpectationValidator.RegexTimeout.TotalMilliseconds);
            return false;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Regex {Pattern} of {Label} is invalid ({Error}); treated as no match", pattern, label, ex.Message);
            return false;
        }
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
