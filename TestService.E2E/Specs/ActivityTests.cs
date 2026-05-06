namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ActivityTests : AuthenticatedTest
{
    [Test]
    public async Task LoadsActivityLog()
    {
        await Page.GotoAsync("/activity");
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await Expect(Page.Locator(".animate-spin")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task RedirectsToLoginWhenUnauthenticated()
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = TestConfig.BaseUrl,
            StorageState = "{\"cookies\":[],\"origins\":[]}",
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/activity");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    [Test]
    public async Task RefreshButtonReloadsActivity()
    {
        await Page.GotoAsync("/activity");
        await Expect(Page.Locator(".animate-spin")).ToHaveCountAsync(0, new() { Timeout = 10_000 });
        var refreshBtn = Page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("refresh", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        await Expect(refreshBtn).ToBeVisibleAsync();
        await refreshBtn.ClickAsync();
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    [Test]
    public async Task ShowsEmptyStateWhenNoActivities()
    {
        await Page.RouteAsync("**/api/activity**", route => route.FulfillAsync(new()
        {
            Status = 200,
            ContentType = "application/json",
            Body = "{\"activities\":[],\"total\":0}",
        }));
        await Page.GotoAsync("/activity");
        await Expect(Page.Locator(".animate-spin")).ToHaveCountAsync(0);
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }
}
