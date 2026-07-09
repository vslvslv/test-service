using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class DashboardTests : AuthenticatedTest
{
    private DashboardPage _dashboard = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dashboard = new DashboardPage(Page);
        await _dashboard.GotoAsync();
    }

    // ── Auth guard ───────────────────────────────────────────────────────────

    [Test]
    public async Task RedirectsToLoginWhenUnauthenticated()
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = TestConfig.BaseUrl,
            StorageState = "{\"cookies\":[],\"origins\":[]}",
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    // ── Hero ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task ShowsWelcomeHeading()
    {
        await Expect(_dashboard.Heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task ShowsOperationsOverviewLabel()
    {
        await Expect(_dashboard.OperationsOverviewLabel).ToBeVisibleAsync();
    }

    [Test]
    public async Task ShowsCurrentBalancePanel()
    {
        await Expect(_dashboard.CurrentBalancePanel).ToBeVisibleAsync();
    }

    // ── Stat cards ───────────────────────────────────────────────────────────

    [Test]
    public async Task ShowsAllStatCardTitles()
    {
        await Expect(_dashboard.TotalSchemasCard).ToBeVisibleAsync();
        await Expect(_dashboard.EnvironmentsCard).ToBeVisibleAsync();
        await Expect(_dashboard.AvailableEntitiesCard).ToBeVisibleAsync();
        await Expect(_dashboard.ConsumedEntitiesCard).ToBeVisibleAsync();
    }

    [Test]
    public async Task TotalSchemasCardNavigatesToSchemasPage()
    {
        await _dashboard.TotalSchemasCard.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas"));
    }

    [Test]
    public async Task EnvironmentsCardNavigatesToEnvironmentsPage()
    {
        await _dashboard.EnvironmentsCard.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/environments"));
    }

    [Test]
    public async Task AvailableEntitiesCardNavigatesToEntitiesPage()
    {
        await _dashboard.AvailableEntitiesCard.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/entities"));
    }

    [Test]
    public async Task ConsumedEntitiesCardNavigatesToEntitiesPage()
    {
        await _dashboard.ConsumedEntitiesCard.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/entities"));
    }

    // ── Recent Schemas ───────────────────────────────────────────────────────

    [Test]
    public async Task ShowsRecentSchemasSectionHeading()
    {
        await Expect(_dashboard.RecentSchemasHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task ViewAllButtonNavigatesToSchemasPage()
    {
        await _dashboard.ViewAllButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas"));
    }

    [Test]
    public async Task SchemaRowNavigatesToSchemaDetailPage()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = """[{"id":"1","entityName":"TestSchema","fields":[],"excludeOnFetch":false}]""",
            }));
        await Page.RouteAsync("**/api/entities/TestSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));

        await _dashboard.GotoAsync();
        await _dashboard.FirstSchemaRow.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas/TestSchema"));
    }

    [Test]
    public async Task EmptyStateShownWhenNoSchemas()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));

        await _dashboard.GotoAsync();

        await Expect(_dashboard.EmptyStateSchemasMessage).ToBeVisibleAsync();
        await Expect(_dashboard.CreateFirstSchemaButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task CreateFirstSchemaButtonNavigatesToNewSchemaPage()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));

        await _dashboard.GotoAsync();
        await _dashboard.CreateFirstSchemaButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas/new"));
    }

    // ── Quick Actions ────────────────────────────────────────────────────────

    [Test]
    public async Task ShowsQuickActionsHeading()
    {
        await Expect(_dashboard.QuickActionsHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task CreateNewSchemaActionNavigatesToNewSchemaPage()
    {
        await _dashboard.CreateNewSchemaAction.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas/new"));
    }

    [Test]
    public async Task ManageEnvironmentsActionNavigatesToEnvironmentsPage()
    {
        await _dashboard.ManageEnvironmentsAction.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/environments"));
    }

    [Test]
    public async Task ViewActivityActionNavigatesToActivityPage()
    {
        await _dashboard.ViewActivityAction.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/activity"));
    }

    // ── System Status ────────────────────────────────────────────────────────

    [Test]
    public async Task ShowsSystemStatusSection()
    {
        await Expect(_dashboard.SystemStatusHeading).ToBeVisibleAsync();
        await Expect(_dashboard.ApiServiceStatus).ToBeVisibleAsync();
        await Expect(_dashboard.DatabaseStatus).ToBeVisibleAsync();
        await Expect(_dashboard.MessageBusStatus).ToBeVisibleAsync();
    }

    // ── Loading ──────────────────────────────────────────────────────────────

    [Test]
    public async Task ShowsSpinnerWhileDataLoads()
    {
        var releaseSchemas = new TaskCompletionSource();
        await Page.RouteAsync("**/api/schemas", async route =>
        {
            await releaseSchemas.Task;
            await route.ContinueAsync();
        });

        await Page.GotoAsync("/");
        await Expect(_dashboard.Spinner).ToBeVisibleAsync();
        releaseSchemas.SetResult();
        await Expect(_dashboard.Spinner).ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }
}
