using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class EntityListTests : AuthenticatedTest
{
    private EntityListPage _entityList = null!;

    private const string SchemaJson =
        """{"id":"1","entityName":"TestSchema","fields":[{"name":"username","type":"string"}],"excludeOnFetch":false}""";
    private const string AutoConsumeSchemaJson =
        """{"id":"2","entityName":"TestSchema","fields":[{"name":"username","type":"string"}],"excludeOnFetch":true}""";
    private const string OneEntityJson =
        """[{"id":"e1","entityType":"TestSchema","fields":{"username":"testuser"},"isConsumed":false}]""";

    [SetUp]
    public async Task SetUp()
    {
        _entityList = new EntityListPage(Page);
        await Page.RouteAsync("**/api/schemas/TestSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SchemaJson }));
        await Page.RouteAsync("**/api/entities/TestSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = OneEntityJson }));
        await _entityList.GotoAsync("TestSchema");
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
        await page.GotoAsync("/entities/TestSchema");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    // ── Page load ─────────────────────────────────────────────────────────────

    [Test]
    public async Task PageLoadsAndSpinnerIsGone()
    {
        await Expect(_entityList.Spinner).ToHaveCountAsync(0);
    }

    [Test]
    public async Task EntityTypeNameIsShownInHeading()
    {
        await Expect(_entityList.EntityTypeHeading).ToHaveTextAsync("TestSchema");
    }

    // ── Stat cards ────────────────────────────────────────────────────────────

    [Test]
    public async Task AllStatCardsAreVisible()
    {
        await Expect(_entityList.EntitiesStatCard).ToBeVisibleAsync();
        await Expect(_entityList.AvailableStatCard).ToBeVisibleAsync();
        await Expect(_entityList.ConsumedStatCard).ToBeVisibleAsync();
        await Expect(_entityList.FieldsStatCard).ToBeVisibleAsync();
    }

    // ── Toolbar and actions ───────────────────────────────────────────────────

    [Test]
    public async Task HeaderActionButtonsAreVisible()
    {
        await Expect(_entityList.ImportButton).ToBeVisibleAsync();
        await Expect(_entityList.ExportButton).ToBeVisibleAsync();
        await Expect(_entityList.CreateEntityButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task SearchInputIsVisible()
    {
        await Expect(_entityList.SearchInput).ToBeVisibleAsync();
    }

    [Test]
    public async Task ColumnsButtonIsVisible()
    {
        await Expect(_entityList.ColumnsButton).ToBeVisibleAsync();
    }

    // ── Error state ───────────────────────────────────────────────────────────

    [Test]
    public async Task ErrorBannerShownWhenSchemaApiFails()
    {
        await Page.RouteAsync("**/api/schemas/BrokenSchema", async route =>
            await route.FulfillAsync(new()
            {
                Status = 500,
                ContentType = "application/json",
                Body = """{"error":"Internal server error"}""",
            }));
        await Page.RouteAsync("**/api/entities/BrokenSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await Page.GotoAsync("/entities/BrokenSchema");
        await Expect(_entityList.ErrorBanner).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    // ── Entity table ──────────────────────────────────────────────────────────

    [Test]
    public async Task EntityRowIsVisibleInTable()
    {
        await Expect(_entityList.EntityRows).ToHaveCountAsync(1);
    }

    [Test]
    public async Task EntityRowShowsAvailableStatus()
    {
        await Expect(Page.GetByText("Available").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task ViewAndDeleteButtonsAreVisiblePerRow()
    {
        await Expect(_entityList.ViewEntityButton).ToBeVisibleAsync();
        await Expect(_entityList.DeleteEntityButton).ToBeVisibleAsync();
    }

    // ── Delete entity ─────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteEntityDialogCanBeDismissed()
    {
        Page.Dialog += async (_, dialog) => await dialog.DismissAsync();
        await _entityList.DeleteEntityButton.ClickAsync();
        await Expect(_entityList.EntityRows).ToHaveCountAsync(1);
    }

    // ── Empty states ──────────────────────────────────────────────────────────

    [Test]
    public async Task EmptyStateShownWithNoEntities()
    {
        await Page.RouteAsync("**/api/schemas/EmptySchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json",
                Body = """{"id":"3","entityName":"EmptySchema","fields":[{"name":"f1","type":"string"}],"excludeOnFetch":false}""" }));
        await Page.RouteAsync("**/api/entities/EmptySchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await _entityList.GotoAsync("EmptySchema");

        await Expect(_entityList.EmptyStateNoEntities).ToBeVisibleAsync();
        await Expect(_entityList.EmptyStateCreateEntityButton).ToBeVisibleAsync();
        await Expect(_entityList.EmptyStateImportDatasetButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task NoMatchMessageShownWhenSearchHasNoResults()
    {
        await _entityList.SearchInput.FillAsync("zzznomatch9876");
        await Expect(_entityList.EmptyStateNoMatchMessage).ToBeVisibleAsync();
    }

    // ── Import modal ──────────────────────────────────────────────────────────

    [Test]
    public async Task ImportModalOpensAndCloses()
    {
        await _entityList.ImportButton.ClickAsync();
        await Expect(_entityList.ImportModalHeading).ToBeVisibleAsync();

        await _entityList.ImportModalCloseButton.ClickAsync();
        await Expect(_entityList.ImportModalHeading).ToBeHiddenAsync();
    }

    // ── Auto-consume features ─────────────────────────────────────────────────

    [Test]
    public async Task ShowConsumedFilterVisibleForAutoConsumeSchemas()
    {
        await Page.RouteAsync("**/api/schemas/AutoSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = AutoConsumeSchemaJson }));
        await Page.RouteAsync("**/api/entities/AutoSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = OneEntityJson }));
        await _entityList.GotoAsync("AutoSchema");
        await Expect(_entityList.ShowConsumedFilterCheckbox).ToBeVisibleAsync();
    }

    // ── Create entity dialog ──────────────────────────────────────────────────

    [Test]
    public async Task CreateEntityDialogOpensOnButtonClick()
    {
        await Page.RouteAsync("**/api/environments", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await _entityList.CreateEntityButton.ClickAsync();
        await Expect(_entityList.CreateDialogTitle).ToBeVisibleAsync();
        await Expect(_entityList.CreateDialogTitle).ToContainTextAsync("TestSchema");
    }

    [Test]
    public async Task CreateEntityDialogClosesOnCancelWithNoData()
    {
        await Page.RouteAsync("**/api/environments", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await _entityList.CreateEntityButton.ClickAsync();
        await Expect(_entityList.CreateDialogTitle).ToBeVisibleAsync();

        // No data entered — cancel navigates away without a confirm dialog.
        await _entityList.DialogCancelButton.ClickAsync();
        await Expect(_entityList.CreateDialogTitle).ToHaveCountAsync(0);
    }

    [Test]
    public async Task CreateEntityDialogRequiredFieldValidation()
    {
        // Use a schema with a required field so the validation triggers.
        const string RequiredFieldSchemaJson =
            """{"id":"req","entityName":"TestSchema","fields":[{"name":"email","type":"string","required":true}],"excludeOnFetch":false}""";
        await Page.RouteAsync("**/api/schemas/RequiredSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = RequiredFieldSchemaJson }));
        await Page.RouteAsync("**/api/entities/RequiredSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await Page.RouteAsync("**/api/environments", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await _entityList.GotoAsync("RequiredSchema");

        await _entityList.EmptyStateCreateEntityButton.ClickAsync();
        // Submit without filling the required field.
        await _entityList.DialogSubmitButton.ClickAsync();
        await Expect(_entityList.DialogFieldError("This field is required")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SuccessfulEntityCreateShowsSuccessBannerAndClosesDialog()
    {
        await Page.RouteAsync("**/api/environments", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "[]" }));
        await Page.RouteAsync("**/api/entities/TestSchema", async route =>
        {
            if (route.Request.Method == "POST")
                await route.FulfillAsync(new()
                {
                    Status = 201,
                    ContentType = "application/json",
                    Body = """{"id":"e2","entityType":"TestSchema","fields":{"username":"newuser"},"isConsumed":false}""",
                });
            else
                await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = OneEntityJson });
        });

        await _entityList.CreateEntityButton.ClickAsync();
        await _entityList.DialogFieldInput("username").FillAsync("newuser");
        await _entityList.DialogSubmitButton.ClickAsync();

        // Dialog should close and success banner should appear.
        await Expect(_entityList.CreateDialogTitle).ToHaveCountAsync(0, new() { Timeout = 5_000 });
        await Expect(_entityList.SuccessBanner).ToBeVisibleAsync();
    }

    // ── Not-found state ───────────────────────────────────────────────────────

    [Test]
    public async Task NotFoundStateShownForUnknownEntityType()
    {
        await Page.RouteAsync("**/api/schemas/nonexistent", async route =>
            await route.FulfillAsync(new() { Status = 404, ContentType = "application/json",
                Body = """{"message":"Not found"}""" }));
        await Page.RouteAsync("**/api/entities/nonexistent", async route =>
            await route.FulfillAsync(new() { Status = 404, ContentType = "application/json",
                Body = """{"message":"Not found"}""" }));
        await _entityList.GotoAsync("nonexistent");
        await Expect(_entityList.NotFoundHeading).ToBeVisibleAsync();
        await Expect(_entityList.BackToEntitiesButton).ToBeVisibleAsync();
    }
}
