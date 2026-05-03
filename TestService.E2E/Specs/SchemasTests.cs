using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class SchemasTests : AuthenticatedTest
{
    private SchemasPage _schemas = null!;

    private const string SingleSchemaJson =
        """[{"id":"1","entityName":"TestSchema","fields":[{"name":"field1","type":"string"}],"excludeOnFetch":false}]""";

    [SetUp]
    public async Task SetUp()
    {
        _schemas = new SchemasPage(Page);
        await _schemas.GotoAsync();
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
        await page.GotoAsync("/schemas");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    // ── Page load ─────────────────────────────────────────────────────────────

    [Test]
    public async Task PageLoadsAndSpinnerIsGone()
    {
        await Expect(_schemas.Spinner).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SearchInputIsVisible()
    {
        await Expect(_schemas.SearchInput).ToBeVisibleAsync();
    }

    [Test]
    public async Task AllSummaryStatLabelsAreVisible()
    {
        await Expect(_schemas.TotalSchemasLabel).ToBeVisibleAsync();
        await Expect(_schemas.AutoConsumeEnabledLabel).ToBeVisibleAsync();
        await Expect(_schemas.TotalFieldsTrackedLabel).ToBeVisibleAsync();
    }

    [Test]
    public async Task AllToolbarControlsAreVisible()
    {
        await Expect(_schemas.SortSelect).ToBeVisibleAsync();
        await Expect(_schemas.ListViewButton).ToBeVisibleAsync();
        await Expect(_schemas.GridViewButton).ToBeVisibleAsync();
        await Expect(_schemas.AutoConsumeFilterCheckbox).ToBeVisibleAsync();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateButtonNavigatesToNewSchemaPage()
    {
        await _schemas.CreateButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas/new"));
    }

    // ── Error state ───────────────────────────────────────────────────────────

    [Test]
    public async Task ErrorBannerShownOnApiFailure()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new()
            {
                Status = 500,
                ContentType = "application/json",
                Body = """{"error":"Internal server error"}""",
            }));
        await Page.GotoAsync("/schemas");
        await Expect(_schemas.ErrorBanner).ToBeVisibleAsync();
    }

    // ── Empty states ──────────────────────────────────────────────────────────

    [Test]
    public async Task EmptyStateShownWhenSchemaListIsEmpty()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await _schemas.GotoAsync();
        await Expect(_schemas.EmptyStateNoSchemasYet).ToBeVisibleAsync();
    }

    [Test]
    public async Task NoMatchMessageShownWhenSearchHasNoResults()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SingleSchemaJson }));
        await _schemas.GotoAsync();
        await _schemas.SearchInput.FillAsync("zzznomatch9876");
        await Expect(_schemas.EmptyStateNoMatchMessage).ToBeVisibleAsync();
    }

    // ── Schema list items ─────────────────────────────────────────────────────

    [Test]
    public async Task SchemaNameHeadingIsVisibleWithMockedData()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SingleSchemaJson }));
        await _schemas.GotoAsync();
        await Expect(_schemas.SchemaNameHeading("TestSchema")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SchemaRowNavigatesToEditPage()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SingleSchemaJson }));
        await Page.RouteAsync("**/api/schemas/TestSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json",
                Body = """{"id":"1","entityName":"TestSchema","fields":[{"name":"field1","type":"string"}],"excludeOnFetch":false}""" }));
        await _schemas.GotoAsync();
        await _schemas.SchemaRowButton("TestSchema").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas/TestSchema"));
    }

    [Test]
    public async Task EditButtonIsVisibleWithMockedData()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SingleSchemaJson }));
        await _schemas.GotoAsync();
        await Expect(_schemas.EditButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task DeleteButtonIsVisibleWithMockedData()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SingleSchemaJson }));
        await _schemas.GotoAsync();
        await Expect(_schemas.DeleteButton).ToBeVisibleAsync();
    }

    // ── Delete dialog ─────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteDialogCanBeDismissed()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SingleSchemaJson }));
        await _schemas.GotoAsync();

        Page.Dialog += async (_, dialog) => await dialog.DismissAsync();
        await _schemas.DeleteButton.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/schemas$"));
        await Expect(_schemas.SchemaNameHeading("TestSchema")).ToBeVisibleAsync();
    }

    // ── View toggle ───────────────────────────────────────────────────────────

    [Test]
    public async Task AutoConsumeFilterCheckboxIsToggleable()
    {
        await Expect(_schemas.AutoConsumeFilterCheckbox).Not.ToBeCheckedAsync();
        await _schemas.AutoConsumeFilterCheckbox.ClickAsync();
        await Expect(_schemas.AutoConsumeFilterCheckbox).ToBeCheckedAsync();
    }
}
