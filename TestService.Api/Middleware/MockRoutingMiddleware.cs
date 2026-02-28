using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Middleware;

public class MockRoutingMiddleware
{
    private readonly RequestDelegate _next;

    public MockRoutingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMockService mockService)
    {
        if (!context.Request.Path.StartsWithSegments("/mock", out var remaining))
        {
            await _next(context);
            return;
        }

        var segments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (segments.Length < 1)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Environment segment is required. Use /mock/{environment}/..." });
            return;
        }

        var environment = segments[0];
        var path = segments.Length == 1 ? "/" : "/" + string.Join('/', segments.Skip(1));

        string? body = null;
        if (context.Request.ContentLength.GetValueOrDefault() > 0 || context.Request.Method is "POST" or "PUT" or "PATCH")
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var query = context.Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var headers = context.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var execution = await mockService.ExecuteAsync(new MockExecutionRequest
        {
            Environment = environment,
            Method = context.Request.Method.ToUpperInvariant(),
            Path = path,
            Query = query,
            Headers = headers,
            QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value! : string.Empty,
            Body = body
        });

        if (execution.DelayMs > 0)
        {
            await Task.Delay(execution.DelayMs, context.RequestAborted);
        }

        context.Response.StatusCode = execution.StatusCode;
        foreach (var header in execution.Headers)
        {
            context.Response.Headers[header.Key] = header.Value;
        }

        if (!string.IsNullOrEmpty(execution.Body))
        {
            if (!context.Response.Headers.ContainsKey("Content-Type"))
            {
                context.Response.ContentType = "application/json";
            }
            await context.Response.WriteAsync(execution.Body);
        }
    }
}
