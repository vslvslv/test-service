using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestService.Api.Emulators;

/// <summary>
/// A provider emulator reachable at <c>/emulators/{provider}/{environment}/{*path}</c>.
/// The middleware turns the HTTP request into an <see cref="EmulatorRequest"/> and writes the <see cref="EmulatorReply"/>.
/// </summary>
public interface IProviderEmulator
{
    /// <summary>The <c>{provider}</c> path segment, for example <c>praxis</c>.</summary>
    string Provider { get; }

    /// <summary>The emulator answers only for environments that have at least one registered merchant.</summary>
    Task<bool> HasEnvironmentAsync(string environment);

    Task<EmulatorReply> HandleAsync(EmulatorRequest request);
}

/// <summary>An incoming emulator request, independent of <c>HttpContext</c>.</summary>
public sealed class EmulatorRequest
{
    public string Environment { get; init; } = string.Empty;

    /// <summary>The path after the environment segment, without a leading slash (for example <c>api/direct-process</c>).</summary>
    public string Path { get; init; } = string.Empty;

    public string Method { get; init; } = "POST";

    public string? Body { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>URL-encoded form fields of the body, when it is a form.</summary>
    public IReadOnlyDictionary<string, string> Form { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Scheme, host and path base of this service (for example <c>http://localhost:5000</c>), used for absolute links.</summary>
    public string BaseUrl { get; init; } = "http://localhost";

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>The emulator's answer: status, content type, body and extra headers (for example <c>Location</c>).</summary>
public sealed class EmulatorReply
{
    public int StatusCode { get; init; } = 200;

    public string ContentType { get; init; } = "application/json; charset=UTF-8";

    public string Body { get; init; } = string.Empty;

    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public JsonNode? Json() => string.IsNullOrEmpty(Body) || !ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
        ? null
        : JsonNode.Parse(Body);

    public static EmulatorReply Message(int statusCode, string message) => new()
    {
        StatusCode = statusCode,
        Body = JsonSerializer.Serialize(new { message })
    };
}
