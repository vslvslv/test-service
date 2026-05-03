using Microsoft.Playwright.NUnit;

namespace TestService.E2E;

public abstract class AuthenticatedTest : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = TestConfig.BaseUrl,
        StorageStatePath = TestConfig.AuthStatePath,
    };
}
