using System.Text.RegularExpressions;
using TestService.Api.Models;

namespace TestService.Api.Services;

/// <summary>
/// Thrown when a mock expectation cannot be stored; <see cref="Field"/> names the offending field.
/// </summary>
public class MockExpectationValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>
/// Save-time checks for mock expectations, run by create, update and duplicate.
/// </summary>
public static class MockExpectationValidator
{
    /// <summary>Time limit for one regex evaluation, both at save and at match time.</summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static void Validate(MockExpectation expectation)
    {
        var matcher = expectation.RequestMatcher;
        if (matcher == null)
        {
            return;
        }

        if (matcher.PathMatchType == PathMatchType.Regex)
        {
            EnsureValidRegex(matcher.Path, "requestMatcher.path");
        }

        if (matcher.BodyMatchType == BodyMatchType.Regex)
        {
            EnsureValidRegex(matcher.Body ?? string.Empty, "requestMatcher.body");
        }
    }

    private static void EnsureValidRegex(string pattern, string field)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new MockExpectationValidationException(field, $"{field} is not a valid regular expression: {ex.Message}");
        }
    }
}
