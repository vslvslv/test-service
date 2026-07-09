using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using TestService.Api.Models;

namespace TestService.Api.Services;

/// <summary>
/// Builds the <see cref="AppInfo"/> payload returned by the info endpoint.
/// </summary>
public interface IAppInfoService
{
    AppInfo GetInfo();
}

/// <summary>
/// Resolves version and runtime metadata from assembly attributes, environment
/// variables (deployment platforms such as Railway inject the commit at runtime),
/// and the current process. All resolution is best-effort and never throws; unknown
/// values fall back to a sensible default ("dev" commit, null build date).
/// </summary>
public sealed class AppInfoService : IAppInfoService
{
    /// <summary>Public HTTP API version. Kept in sync with the Swagger document in Program.cs.</summary>
    public const string ApiVersion = "v1";

    private const string ApplicationName = "Test Service API";
    private const string UnknownCommit = "dev";
    private const int ShortCommitLength = 7;

    // Checked in order; the first non-empty value wins. RAILWAY_GIT_COMMIT_SHA is
    // provided by Railway at runtime; the others cover common CI conventions.
    private static readonly string[] CommitEnvVars =
    {
        "GIT_COMMIT", "RAILWAY_GIT_COMMIT_SHA", "SOURCE_COMMIT", "SOURCE_VERSION"
    };

    private static readonly string[] BuildDateEnvVars = { "BUILD_DATE", "BUILD_TIMESTAMP" };

    private readonly IHostEnvironment _environment;
    private readonly string? _informationalVersion;
    private readonly string? _commitMetadata;
    private readonly string? _buildDateMetadata;
    private readonly DateTime _startTimeUtc;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<string, string?> _getEnvVar;

    public AppInfoService(IHostEnvironment environment)
        : this(
            environment,
            GetInformationalVersion(typeof(AppInfoService).Assembly),
            GetAssemblyMetadata(typeof(AppInfoService).Assembly, "CommitHash"),
            GetAssemblyMetadata(typeof(AppInfoService).Assembly, "BuildTimestampUtc"),
            GetProcessStartUtc(),
            static () => DateTime.UtcNow,
            System.Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Test seam: injects the raw build metadata, clock, and environment lookup.</summary>
    internal AppInfoService(
        IHostEnvironment environment,
        string? informationalVersion,
        string? commitMetadata,
        string? buildDateMetadata,
        DateTime startTimeUtc,
        Func<DateTime> utcNow,
        Func<string, string?> getEnvVar)
    {
        _environment = environment;
        _informationalVersion = informationalVersion;
        _commitMetadata = commitMetadata;
        _buildDateMetadata = buildDateMetadata;
        _startTimeUtc = startTimeUtc;
        _utcNow = utcNow;
        _getEnvVar = getEnvVar;
    }

    public AppInfo GetInfo()
    {
        var nowUtc = _utcNow();
        var uptime = nowUtc - _startTimeUtc;
        if (uptime < TimeSpan.Zero)
        {
            uptime = TimeSpan.Zero;
        }

        return new AppInfo
        {
            Name = ApplicationName,
            Version = ResolveVersion(),
            Commit = ResolveCommit(),
            BuildDateUtc = ResolveBuildDate(),
            Environment = _environment.EnvironmentName,
            ApiVersion = ApiVersion,
            Runtime = RuntimeInformation.FrameworkDescription,
            ServerTimeUtc = FormatUtc(nowUtc),
            Uptime = FormatUptime(uptime),
            UptimeSeconds = (long)uptime.TotalSeconds
        };
    }

    private string ResolveVersion()
    {
        if (!string.IsNullOrWhiteSpace(_informationalVersion))
        {
            // The SDK appends "+<sourceRevisionId>" build metadata; strip it for display.
            return StripBuildMetadata(_informationalVersion);
        }

        return "0.0.0";
    }

    private string ResolveCommit()
    {
        foreach (var name in CommitEnvVars)
        {
            var value = _getEnvVar(name)?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Shorten(value);
            }
        }

        if (!string.IsNullOrWhiteSpace(_commitMetadata))
        {
            return Shorten(_commitMetadata.Trim());
        }

        // The .NET SDK auto-appends "+<sourceRevisionId>" (the git commit) to the
        // informational version when built inside a git repo — use it as a fallback.
        var embeddedCommit = ExtractBuildMetadata(_informationalVersion)?.Trim();
        return string.IsNullOrWhiteSpace(embeddedCommit) ? UnknownCommit : Shorten(embeddedCommit);
    }

    private string? ResolveBuildDate()
    {
        foreach (var name in BuildDateEnvVars)
        {
            // Trim so trailing whitespace/newlines (common when tooling injects env vars
            // from files) don't leak into the ISO-8601 value surfaced by the API.
            var value = _getEnvVar(name)?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.IsNullOrWhiteSpace(_buildDateMetadata) ? null : _buildDateMetadata.Trim();
    }

    /// <summary>Returns the version portion before the "+build.metadata" suffix.</summary>
    internal static string StripBuildMetadata(string informationalVersion)
    {
        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }

    /// <summary>Returns the "+build.metadata" suffix (the git commit), or null when absent.</summary>
    internal static string? ExtractBuildMetadata(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return null;
        }

        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 && plusIndex < informationalVersion.Length - 1
            ? informationalVersion[(plusIndex + 1)..]
            : null;
    }

    internal static string Shorten(string commit) =>
        commit.Length > ShortCommitLength ? commit[..ShortCommitLength] : commit;

    internal static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        if (uptime.TotalMinutes >= 1)
        {
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        }

        return $"{(int)uptime.TotalSeconds}s";
    }

    private static string FormatUtc(DateTime value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string? GetInformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    private static string? GetAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    private static DateTime GetProcessStartUtc()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime();
        }
        catch
        {
            // Process metrics can be unavailable in constrained sandboxes; degrade gracefully.
            return DateTime.UtcNow;
        }
    }
}
