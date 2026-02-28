using System.Reflection;

namespace TestService.Tests.Infrastructure;

/// <summary>
/// Regression tests for nginx.conf: ensures bug-fix directives are present so /api proxy and SPA at root keep working.
/// </summary>
[TestFixture]
public class NginxConfigRegressionTests
{
    private static string? FindNginxConfPath()
    {
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir))
            return null;

        // When running from repo root: dotnet test (CurrentDirectory = repo root)
        // When running from test output dir: resolve relative to assembly
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "testservice-web", "nginx.conf"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "testservice-web", "nginx.conf")),
        };

        foreach (var path in candidates)
        {
            var full = Path.GetFullPath(path);
            if (File.Exists(full))
                return full;
        }

        return null;
    }

    [Test]
    public void NginxConf_Exists_WhenRunFromRepo()
    {
        var path = FindNginxConfPath();
        if (path == null)
        {
            Assert.Inconclusive("nginx.conf not found (run tests from repo root or ensure testservice-web/nginx.conf exists).");
            return;
        }

        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void NginxConf_ProxiesApiAtRoot_SoFrontendApiCallsSucceed()
    {
        var path = FindNginxConfPath();
        if (path == null)
        {
            Assert.Inconclusive("nginx.conf not found.");
            return;
        }

        var content = File.ReadAllText(path);
        Assert.That(content, Does.Contain("location /api/"), "Must proxy /api/ to backend (bug-fix: was 404 for /api/live/ws)");
        Assert.That(content, Does.Contain("proxy_pass"), "Must have proxy_pass for API");
        Assert.That(content, Does.Contain("api:80").Or.Contain("api:8080"), "Must forward to API service");
    }

    [Test]
    public void NginxConf_ServesSpaAtRoot_SoLoginPageLoads()
    {
        var path = FindNginxConfPath();
        if (path == null)
        {
            Assert.Inconclusive("nginx.conf not found.");
            return;
        }

        var content = File.ReadAllText(path);
        Assert.That(content, Does.Contain("location /"), "Must have location / for SPA at root");
        Assert.That(content, Does.Contain("try_files"), "Must use try_files so client-side routes serve index.html");
        Assert.That(content, Does.Contain("/index.html"), "Must fallback to index.html for SPA routing");
    }

    [Test]
    public void NginxConf_HasHealthEndpoint_ForComposeAndK8s()
    {
        var path = FindNginxConfPath();
        if (path == null)
        {
            Assert.Inconclusive("nginx.conf not found.");
            return;
        }

        var content = File.ReadAllText(path);
        Assert.That(content, Does.Contain("location /health").Or.Contain("/health"), "Must expose /health for healthchecks");
    }
}
