using Microsoft.AspNetCore.WebUtilities;

namespace TestService.Api.Emulators;

/// <summary>
/// Anonymous entry point of the provider emulators: <c>/emulators/{provider}/{environment}/{*path}</c>.
/// Answers 404 with a JSON <c>message</c> for an unknown provider or an environment without merchants.
/// </summary>
public class EmulatorMiddleware
{
    public const string PathPrefix = "/emulators";

    private readonly RequestDelegate _next;

    public EmulatorMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IEnumerable<IProviderEmulator> emulators)
    {
        if (!context.Request.Path.StartsWithSegments(PathPrefix, out var remaining))
        {
            await _next(context);
            return;
        }

        var segments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length < 1)
        {
            await WriteAsync(context, EmulatorReply.Message(StatusCodes.Status404NotFound,
                "Provider segment is required. Use /emulators/{provider}/{environment}/..."));
            return;
        }

        var emulator = emulators.FirstOrDefault(e => string.Equals(e.Provider, segments[0], StringComparison.OrdinalIgnoreCase));
        if (emulator is null)
        {
            await WriteAsync(context, EmulatorReply.Message(StatusCodes.Status404NotFound, $"Unknown emulator provider '{segments[0]}'."));
            return;
        }

        if (segments.Length < 2)
        {
            await WriteAsync(context, EmulatorReply.Message(StatusCodes.Status404NotFound,
                $"Environment segment is required. Use /emulators/{emulator.Provider}/{{environment}}/..."));
            return;
        }

        var environment = segments[1];
        if (!await emulator.HasEnvironmentAsync(environment))
        {
            await WriteAsync(context, EmulatorReply.Message(StatusCodes.Status404NotFound,
                $"No {emulator.Provider} merchant is registered for environment '{environment}'."));
            return;
        }

        string? body = null;
        if (context.Request.ContentLength.GetValueOrDefault() > 0 || context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            using var reader = new StreamReader(context.Request.Body);
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }

        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (body is not null && context.Request.HasFormContentType)
        {
            foreach (var (key, value) in QueryHelpers.ParseQuery(body))
            {
                form[key] = value.ToString();
            }
        }

        var request = new EmulatorRequest
        {
            Environment = environment,
            Path = string.Join('/', segments.Skip(2)),
            Method = context.Request.Method.ToUpperInvariant(),
            Body = body,
            Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Query = context.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase),
            Form = form,
            BaseUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}"
        };

        await WriteAsync(context, await emulator.HandleAsync(request));
    }

    private static async Task WriteAsync(HttpContext context, EmulatorReply reply)
    {
        context.Response.StatusCode = reply.StatusCode;
        foreach (var (name, value) in reply.Headers)
        {
            context.Response.Headers[name] = value;
        }
        context.Response.ContentType = reply.ContentType;
        if (!string.IsNullOrEmpty(reply.Body))
        {
            await context.Response.WriteAsync(reply.Body);
        }
    }
}
