using Microsoft.Playwright;

namespace TestService.E2E;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public async Task SetUpAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync(new() { BaseURL = TestConfig.BaseUrl });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/login");
        await page.GetByLabel("Username").FillAsync(TestConfig.Username);
        await page.GetByLabel("Password").FillAsync(TestConfig.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });

        Directory.CreateDirectory(".auth");
        await context.StorageStateAsync(new() { Path = TestConfig.AuthStatePath });
    }
}
