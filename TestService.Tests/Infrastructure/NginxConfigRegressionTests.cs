namespace TestService.Tests.Infrastructure;

/// <summary>
/// Regression tests for nginx.conf: ensures bug-fix directives are present so /api proxy and SPA at root keep working.
/// </summary>
[TestFixture]
public class NginxConfigRegressionTests
{
    private string _nginxConfContent = string.Empty;

    [OneTimeSetUp]
    public void LoadNginxConf()
    {
        var path = FindNginxConfPath()
            ?? throw new InvalidOperationException(
                "nginx.conf not found. Run tests from repo root or ensure testservice-web/nginx.conf exists. " +
                $"Searched relative to current dir ({Directory.GetCurrentDirectory()}) " +
                $"and assembly base ({AppContext.BaseDirectory}).");
        _nginxConfContent = File.ReadAllText(path);
    }

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
    public void NginxConf_ProxiesApiAtRoot_SoFrontendApiCallsSucceed()
    {
        Assert.That(_nginxConfContent, Does.Contain("location /api/"), "Must proxy /api/ to backend (bug-fix: was 404 for /api/live/ws)");
        Assert.That(_nginxConfContent, Does.Contain("proxy_pass"), "Must have proxy_pass for API");
        Assert.That(_nginxConfContent, Does.Contain("api:80"), "Must forward to API service on port 80");
    }

    [Test]
    public void NginxConf_ServesSpaAtRoot_SoLoginPageLoads()
    {
        Assert.That(_nginxConfContent, Does.Contain("location /"), "Must have location / for SPA at root");
        Assert.That(_nginxConfContent, Does.Contain("try_files"), "Must use try_files so client-side routes serve index.html");
        Assert.That(_nginxConfContent, Does.Contain("/index.html"), "Must fallback to index.html for SPA routing");
    }

    [Test]
    public void NginxConf_HasHealthEndpoint_ForComposeAndK8s()
    {
        Assert.That(_nginxConfContent, Does.Contain("/health"), "Must expose /health for healthchecks");
    }
}
