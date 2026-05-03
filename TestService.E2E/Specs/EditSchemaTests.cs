using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class EditSchemaTests : AuthenticatedTest
{
    private EditSchemaPage _edit = null!;

    private const string SchemaJson =
        """{"id":"1","entityName":"TestSchema","fields":[{"name":"field1","type":"string","required":false}],"excludeOnFetch":false}""";

    private const string AutoConsumeSchemaJson =
        """{"id":"2","entityName":"TestSchema","fields":[{"name":"field1","type":"string","required":false}],"excludeOnFetch":true}""";

    [SetUp]
    public async Task SetUp()
    {
        _edit = new EditSchemaPage(Page);
    }

    private async Task MockSchemaAndGoto(string body = SchemaJson)
    {
        await Page.RouteAsync("**/api/schemas/TestSchema", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = body }));
        await _edit.GotoAsync("TestSchema");
    }

    // ── Loading ───────────────────────────────────────────────────────────────

    [Test]
    public async Task SpinnerAppearsAndDisappearsOnLoad()
    {
        await Page.RouteAsync("**/api/schemas/SlowSchema", async route =>
        {
            await Task.Delay(600);
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SchemaJson });
        });
        await Page.GotoAsync("/schemas/SlowSchema");
        await Expect(_edit.Spinner).ToBeVisibleAsync();
        await Expect(_edit.Spinner).ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }

    // ── Schema metadata ───────────────────────────────────────────────────────

    [Test]
    public async Task SchemaNameIsDisplayedInHeading()
    {
        await MockSchemaAndGoto();
        await Expect(_edit.SchemaNameHeading).ToHaveTextAsync("TestSchema");
    }

    [Test]
    public async Task SchemaNameInputIsDisabled()
    {
        await MockSchemaAndGoto();
        await Expect(_edit.SchemaNameInput).ToBeDisabledAsync();
        await Expect(_edit.SchemaNameInput).ToHaveValueAsync("TestSchema");
    }

    [Test]
    public async Task AutoConsumeCheckboxReflectsSchemaState()
    {
        await MockSchemaAndGoto(AutoConsumeSchemaJson);
        await Expect(_edit.AutoConsumeCheckbox).ToBeCheckedAsync();
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    [Test]
    public async Task FieldsAreLoadedFromSchema()
    {
        await MockSchemaAndGoto();
        await Expect(_edit.FieldNameInput(1)).ToHaveValueAsync("field1");
    }

    [Test]
    public async Task AddFieldButtonAddsNewField()
    {
        await MockSchemaAndGoto();
        await _edit.AddFieldButton.ClickAsync();
        await Expect(_edit.FieldNameInput(2)).ToBeVisibleAsync();
    }

    [Test]
    public async Task CannotRemoveLastField()
    {
        await Page.RouteAsync("**/api/schemas/SingleField", async route =>
            await route.FulfillAsync(new() { Status = 200, ContentType = "application/json",
                Body = """{"id":"1","entityName":"SingleField","fields":[{"name":"f1","type":"string"}],"excludeOnFetch":false}""" }));
        await _edit.GotoAsync("SingleField");

        Page.Dialog += async (_, dialog) =>
        {
            Assert.That(dialog.Message, Does.Contain("at least one field"));
            await dialog.AcceptAsync();
        };
        await _edit.FieldRemoveButton(1).ClickAsync();
        await Expect(_edit.FieldNameInput(1)).ToBeVisibleAsync();
    }

    // ── Cancel behaviour ──────────────────────────────────────────────────────

    [Test]
    public async Task CancelAlwaysShowsConfirmDialog()
    {
        await MockSchemaAndGoto();
        var dialogShown = false;
        Page.Dialog += async (_, dialog) =>
        {
            dialogShown = true;
            await dialog.DismissAsync();
        };
        await _edit.CancelButton.ClickAsync();
        Assert.That(dialogShown, Is.True);
    }

    [Test]
    public async Task CancelDismissedKeepsUserOnEditPage()
    {
        await MockSchemaAndGoto();
        Page.Dialog += async (_, dialog) => await dialog.DismissAsync();
        await _edit.CancelButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/schemas/TestSchema"));
    }

    [Test]
    public async Task CancelConfirmedNavigatesToSchemas()
    {
        await MockSchemaAndGoto();
        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await _edit.CancelButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/schemas$"));
    }

    // ── Validation and save ───────────────────────────────────────────────────

    [Test]
    public async Task EmptyFieldNameShowsErrorBanner()
    {
        await MockSchemaAndGoto();
        await _edit.FieldNameInput(1).ClearAsync();
        await _edit.SaveButton.ClickAsync();
        await Expect(_edit.ErrorBanner).ToBeVisibleAsync();
    }

    [Test]
    public async Task SaveSuccessNavigatesToSchemas()
    {
        await Page.RouteAsync("**/api/schemas/TestSchema", async route =>
        {
            if (route.Request.Method == "PUT")
                await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SchemaJson });
            else
                await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SchemaJson });
        });
        await _edit.GotoAsync("TestSchema");
        await _edit.SaveButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/schemas$"),
            new() { Timeout = 10_000 });
    }

    // ── Delete schema ─────────────────────────────────────────────────────────

    [Test]
    public async Task DeleteDialogCanBeDismissed()
    {
        await MockSchemaAndGoto();
        Page.Dialog += async (_, dialog) => await dialog.DismissAsync();
        await _edit.DeleteSchemaButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/schemas/TestSchema"));
    }

    [Test]
    public async Task DeleteConfirmedNavigatesToSchemas()
    {
        await Page.RouteAsync("**/api/schemas/TestSchema", async route =>
        {
            if (route.Request.Method == "DELETE")
                await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "{}" });
            else
                await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = SchemaJson });
        });
        await _edit.GotoAsync("TestSchema");

        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await _edit.DeleteSchemaButton.ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/schemas$"),
            new() { Timeout = 10_000 });
    }

    // ── Not-found state ───────────────────────────────────────────────────────

    [Test]
    public async Task NotFoundStateShownWhenSchemaDoesNotExist()
    {
        await Page.RouteAsync("**/api/schemas/nonexistent", async route =>
            await route.FulfillAsync(new() { Status = 404, ContentType = "application/json",
                Body = """{"message":"Schema not found"}""" }));
        await _edit.GotoAsync("nonexistent");
        await Expect(_edit.NotFoundHeading).ToBeVisibleAsync();
        await Expect(_edit.BackToSchemasButton).ToBeVisibleAsync();
    }
}
