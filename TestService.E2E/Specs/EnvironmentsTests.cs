namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class EnvironmentsTests : AuthenticatedTest
{
    [Test]
    public async Task LoadsEnvironmentList()
    {
        await Page.GotoAsync("/environments");
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
        await page.GotoAsync("/environments");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    [Test]
    public async Task SearchInputFiltersEnvironments()
    {
        await Page.GotoAsync("/environments");
        await Page.Locator(".animate-spin").WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 8_000 });
        var search = Page.GetByPlaceholder("Search environments...");
        await Expect(search).ToBeVisibleAsync();
        await search.FillAsync("zzznomatch");
        await Expect(search).ToHaveValueAsync("zzznomatch");
    }

    [Test]
    public async Task ShowsErrorWhenApiReturns500()
    {
        await Page.RouteAsync("**/api/environments", route => route.FulfillAsync(new()
        {
            Status = 500,
            ContentType = "application/json",
            Body = "{\"error\":\"Server error\"}",
        }));
        await Page.GotoAsync("/environments");
        await Expect(Page.GetByText(new System.Text.RegularExpressions.Regex("error|failed|unable", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).First).ToBeVisibleAsync();
    }
}
