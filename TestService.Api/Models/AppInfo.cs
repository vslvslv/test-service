namespace TestService.Api.Models;

/// <summary>
/// Version and runtime information about the running service, surfaced to end users
/// through the About dialog (GET /api/info). Contains only non-sensitive fields.
/// </summary>
public sealed record AppInfo
{
    /// <summary>Human-readable application name.</summary>
    public required string Name { get; init; }

    /// <summary>Semantic version (e.g. "1.0.0"), without any build-metadata suffix.</summary>
    public required string Version { get; init; }

    /// <summary>Short git commit the build was produced from, or "dev" when unknown.</summary>
    public required string Commit { get; init; }

    /// <summary>ISO-8601 UTC timestamp of when the build was produced, or null when unknown.</summary>
    public string? BuildDateUtc { get; init; }

    /// <summary>Hosting environment name (e.g. "Production", "Development").</summary>
    public required string Environment { get; init; }

    /// <summary>Public HTTP API version served by this build (matches the OpenAPI document).</summary>
    public required string ApiVersion { get; init; }

    /// <summary>.NET runtime the service is running on (framework description).</summary>
    public required string Runtime { get; init; }

    /// <summary>Current server time as an ISO-8601 UTC string.</summary>
    public required string ServerTimeUtc { get; init; }

    /// <summary>Human-readable process uptime (e.g. "3d 4h 5m").</summary>
    public required string Uptime { get; init; }

    /// <summary>Process uptime in whole seconds.</summary>
    public required long UptimeSeconds { get; init; }
}
