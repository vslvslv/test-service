using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class EntitiesTests : AuthenticatedTest
{
    private EntitiesPage _entities = null!;

    private const string OneSchemaJson =
        """[{"id":"1","entityName":"TestSchema","fields":[{"name":"username","type":"string"}],"excludeOnFetch":false}]""";
    private const string OneEntityJson =
        """[{"id":"e1","entityType":"TestSchema","fields":{"username":"testuser"},"isConsumed":false}]""";

    [SetUp]
    public void SetUp()
    {
        _entities = new EntitiesPage(Page);
    }

    private async Task MockDefaultDataAndGoto()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = OneSchemaJson }));
        await Page.RouteAsync("**/api/entities/TestSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = OneEntityJson }));
        await _entities.GotoAsync();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Test]
    public async Task RedirectsToLoginWhenUnauthenticated()
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = TestConfig.BaseUrl,
            StorageState = "{\"cookies\":[],\"origins\":[]}",
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/entities");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    // ── Page load ─────────────────────────────────────────────────────────────

    [Test]
    public async Task PageLoadsAndSpinnerIsGone()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.Spinner).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SearchInputIsVisible()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.SearchInput).ToBeVisibleAsync();
    }

    // ── Hero stat cards ───────────────────────────────────────────────────────

    [Test]
    public async Task HeroStatCardsAreVisible()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.EntityTypesStatCard).ToBeVisibleAsync();
        await Expect(_entities.AvailableStatCard).ToBeVisibleAsync();
        await Expect(_entities.ConsumedStatCard).ToBeVisibleAsync();
    }

    // ── Aggregate stats panel ─────────────────────────────────────────────────

    [Test]
    public async Task AggregateStatLabelsAreVisible()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.TotalEntityRecordsLabel).ToBeVisibleAsync();
        await Expect(_entities.ReadyForAllocationLabel).ToBeVisibleAsync();
        await Expect(_entities.ExhaustedOrUsedLabel).ToBeVisibleAsync();
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    [Test]
    public async Task AutoConsumeFilterCheckboxIsVisible()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.AutoConsumeFilterCheckbox).ToBeVisibleAsync();
    }

    [Test]
    public async Task AutoConsumeFilterCheckboxIsToggleable()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.AutoConsumeFilterCheckbox).Not.ToBeCheckedAsync();
        await _entities.AutoConsumeFilterCheckbox.ClickAsync();
        await Expect(_entities.AutoConsumeFilterCheckbox).ToBeCheckedAsync();
    }

    // ── Error state ───────────────────────────────────────────────────────────

    [Test]
    public async Task ErrorBannerShownWhenSchemasApiFails()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new()
            {
                Status = 500,
                ContentType = "application/json",
                Body = """{"error":"Internal server error"}""",
            }));
        await Page.GotoAsync("/entities");
        await Expect(_entities.ErrorBanner).ToBeVisibleAsync();
    }

    // ── Empty states ──────────────────────────────────────────────────────────

    [Test]
    public async Task EmptyStateShownWhenNoSchemas()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await _entities.GotoAsync();
        await Expect(_entities.EmptyStateNoEntityTypes).ToBeVisibleAsync();
    }

    [Test]
    public async Task NoMatchMessageShownWhenSearchYieldsNoResults()
    {
        await MockDefaultDataAndGoto();
        await _entities.SearchInput.FillAsync("zzznomatch9876");
        await Expect(_entities.EmptyStateNoMatchMessage).ToBeVisibleAsync();
    }

    // ── Entity type rows ──────────────────────────────────────────────────────

    [Test]
    public async Task EntityTypeRowIsVisible()
    {
        await MockDefaultDataAndGoto();
        await Expect(_entities.EntityTypeHeading("TestSchema")).ToBeVisibleAsync();
    }

    [Test]
    public async Task EntityTypeRowNavigatesToWorkspace()
    {
        await MockDefaultDataAndGoto();
        await Page.RouteAsync("**/api/schemas/TestSchema", async route =>
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = """{"id":"1","entityName":"TestSchema","fields":[{"name":"username","type":"string"}],"excludeOnFetch":false}""",
            }));
        await _entities.EntityTypeRow("TestSchema").ClickAsync();
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/entities/TestSchema"));
    }
}
