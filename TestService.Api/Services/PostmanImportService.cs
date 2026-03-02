using System.Text.Json;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IPostmanImportService
{
    IReadOnlyList<MockExpectation> ParseCollection(Stream stream, string targetEnvironment, string? pathPrefix = null);
}

public class PostmanImportService : IPostmanImportService
{
    public IReadOnlyList<MockExpectation> ParseCollection(Stream stream, string targetEnvironment, string? pathPrefix = null)
    {
        using var doc = JsonDocument.Parse(stream);
        var list = new List<MockExpectation>();
        var root = doc.RootElement;
        if (!root.TryGetProperty("item", out var itemsEl))
            return list;

        var prefix = string.IsNullOrWhiteSpace(pathPrefix) ? null : pathPrefix.Trim();
        if (!string.IsNullOrEmpty(prefix) && !prefix.StartsWith('/'))
            prefix = "/" + prefix;

        ParseItems(itemsEl, targetEnvironment, prefix, list, 0);
        return list;
    }

    private static void ParseItems(JsonElement itemsEl, string targetEnvironment, string? pathPrefix, List<MockExpectation> list, int basePriority)
    {
        if (itemsEl.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in itemsEl.EnumerateArray())
        {
            if (item.TryGetProperty("request", out var requestEl))
            {
                var expectation = ParseRequest(item, requestEl, targetEnvironment, pathPrefix, basePriority + index);
                if (expectation != null)
                {
                    list.Add(expectation);
                    index++;
                }
            }
            else if (item.TryGetProperty("item", out var subItems))
            {
                ParseItems(subItems, targetEnvironment, pathPrefix, list, basePriority + index);
            }
        }
    }

    private static MockExpectation? ParseRequest(JsonElement item, JsonElement requestEl, string targetEnvironment, string? pathPrefix, int priority)
    {
        var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(name))
            name = "Imported";

        string? method = null;
        if (requestEl.TryGetProperty("method", out var methodEl))
            method = methodEl.GetString()?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(method))
            method = "GET";

        var path = GetPathFromUrl(requestEl);
        if (string.IsNullOrEmpty(path))
            path = "/";
        if (!path.StartsWith('/'))
            path = "/" + path;
        if (!string.IsNullOrEmpty(pathPrefix))
            path = pathPrefix.TrimEnd('/') + path;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (requestEl.TryGetProperty("header", out var headerEl) && headerEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var h in headerEl.EnumerateArray())
            {
                var key = h.TryGetProperty("key", out var k) ? k.GetString() : null;
                var value = h.TryGetProperty("value", out var v) ? v.GetString() : null;
                if (!string.IsNullOrEmpty(key))
                    headers[key!] = value ?? "";
            }
        }

        string? body = null;
        if (requestEl.TryGetProperty("body", out var bodyEl))
        {
            if (bodyEl.TryGetProperty("mode", out var modeEl) && modeEl.GetString() == "raw" && bodyEl.TryGetProperty("raw", out var rawEl))
                body = rawEl.GetString();
        }

        var status = 200;
        var responseBody = "";
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (requestEl.TryGetProperty("response", out var responseArr) && responseArr.ValueKind == JsonValueKind.Array)
        {
            var first = responseArr.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                if (first.TryGetProperty("code", out var codeEl))
                    status = codeEl.TryGetInt32(out var c) ? c : 200;
                if (first.TryGetProperty("body", out var bodyResp))
                    responseBody = bodyResp.GetString() ?? "";
                if (first.TryGetProperty("header", out var headerResp) && headerResp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in headerResp.EnumerateArray())
                    {
                        var key = h.TryGetProperty("key", out var k) ? k.GetString() : null;
                        var value = h.TryGetProperty("value", out var v) ? v.GetString() : null;
                        if (!string.IsNullOrEmpty(key))
                            responseHeaders[key!] = value ?? "";
                    }
                }
            }
        }

        return new MockExpectation
        {
            Id = null,
            Environment = targetEnvironment,
            Name = name,
            Priority = priority,
            Enabled = true,
            RequestMatcher = new MockRequestMatcher
            {
                Method = method,
                Path = path,
                PathMatchType = PathMatchType.Exact,
                Body = body,
                BodyMatchType = string.IsNullOrEmpty(body) ? BodyMatchType.Any : BodyMatchType.Exact,
                Headers = headers
            },
            ResponseTemplate = new MockResponseTemplate
            {
                Status = status,
                Body = responseBody,
                Headers = responseHeaders
            },
            Times = new MockTimes { Unlimited = true }
        };
    }

    private static string? GetPathFromUrl(JsonElement requestEl)
    {
        if (!requestEl.TryGetProperty("url", out var urlEl))
            return null;

        if (urlEl.ValueKind == JsonValueKind.String)
        {
            var raw = urlEl.GetString();
            if (string.IsNullOrEmpty(raw))
                return null;
            try
            {
                var uri = new Uri(raw);
                return uri.AbsolutePath;
            }
            catch
            {
                return raw.Contains('/') ? "/" + string.Join("/", raw.Split('/').Skip(3)) : raw;
            }
        }

        if (urlEl.ValueKind == JsonValueKind.Object)
        {
            if (urlEl.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.Array)
            {
                var segments = pathEl.EnumerateArray()
                    .Select(s => s.GetString())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();
                return "/" + string.Join("/", segments);
            }
            if (urlEl.TryGetProperty("raw", out var rawEl))
                return GetPathFromUrlRaw(rawEl.GetString());
        }

        return null;
    }

    private static string? GetPathFromUrlRaw(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return null;
        try
        {
            var uri = new Uri(raw);
            return uri.AbsolutePath;
        }
        catch
        {
            return "/";
        }
    }
}
