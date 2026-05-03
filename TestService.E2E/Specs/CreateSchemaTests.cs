using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class CreateSchemaTests : AuthenticatedTest
{
    private CreateSchemaPage _create = null!;

    [SetUp]
    public async Task SetUp()
    {
        _create = new CreateSchemaPage(Page);
        await _create.GotoAsync();
    }

    // ── Page structure ────────────────────────────────────────────────────────

    [Test]
    public async Task PageHeadingIsVisible()
    {
        await Expect(_create.Heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task SchemaNameInputIsVisible()
    {
        await Expect(_create.SchemaNameInput).ToBeVisibleAsync();
    }

    [Test]
    public async Task AutoConsumeCheckboxIsVisible()
    {
        await Expect(_create.AutoConsumeCheckbox).ToBeVisibleAsync();
    }

    [Test]
    public async Task FirstFieldIsRenderedByDefault()
    {
        await Expect(_create.FieldNameInput(1)).ToBeVisibleAsync();
        await Expect(_create.FieldTypeSelect(1)).ToBeVisibleAsync();
    }

    // ── Field builder ─────────────────────────────────────────────────────────

    [Test]
    public async Task AddFieldButtonAddsANewField()
    {
        await _create.AddFieldButton.ClickAsync();
        await Expect(_create.FieldNameInput(2)).ToBeVisibleAsync();
    }

    [Test]
    public async Task RemoveFieldButtonRemovesTheField()
    {
        await _create.AddFieldButton.ClickAsync();
        await Expect(_create.FieldNameInput(2)).ToBeVisibleAsync();

        await _create.FieldRemoveButton(2).ClickAsync();
        await Expect(_create.FieldNameInput(2)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task CannotRemoveLastField()
    {
        Page.Dialog += async (_, dialog) =>
        {
            Assert.That(dialog.Message, Does.Contain("at least one field"));
            await dialog.AcceptAsync();
        };
        await _create.FieldRemoveButton(1).ClickAsync();
        // Field 1 is still present after the alert is dismissed.
        await Expect(_create.FieldNameInput(1)).ToBeVisibleAsync();
    }

    // ── Cancel behaviour ──────────────────────────────────────────────────────

    [Test]
    public async Task CancelWithNoChangesNavigatesBackWithoutConfirm()
    {
        // No dialog should appear — page navigates immediately.
        await _create.CancelButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/schemas$"));
    }

    [Test]
    public async Task CancelWithChangesDismissedKeepsUserOnPage()
    {
        await _create.SchemaNameInput.FillAsync("my-schema");

        Page.Dialog += async (_, dialog) => await dialog.DismissAsync();
        await _create.CancelButton.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas/new"));
    }

    [Test]
    public async Task CancelWithChangesConfirmedNavigatesToSchemas()
    {
        await _create.SchemaNameInput.FillAsync("my-schema");

        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await _create.CancelButton.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/schemas$"));
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public async Task InvalidSchemaNameShowsErrorBanner()
    {
        // Fill an invalid name (space is not in the allowed charset).
        await _create.SchemaNameInput.FillAsync("my schema!");
        await _create.FieldNameInput(1).FillAsync("field1");
        await _create.SubmitButton.ClickAsync();
        await Expect(_create.ErrorBanner).ToBeVisibleAsync();
    }

    [Test]
    public async Task DuplicateFieldNamesShowsErrorBanner()
    {
        await _create.SchemaNameInput.FillAsync("valid-schema");
        await _create.FieldNameInput(1).FillAsync("duplicateName");
        await _create.AddFieldButton.ClickAsync();
        await _create.FieldNameInput(2).FillAsync("duplicateName");
        await _create.SubmitButton.ClickAsync();
        await Expect(_create.ErrorBanner).ToBeVisibleAsync();
    }

    // ── Successful submit ─────────────────────────────────────────────────────

    [Test]
    public async Task SuccessfulCreateNavigatesToSchemasList()
    {
        await Page.RouteAsync("**/api/schemas", async route =>
        {
            if (route.Request.Method == "POST")
                await route.FulfillAsync(new()
                {
                    Status = 201,
                    ContentType = "application/json",
                    Body = """{"id":"1","entityName":"TestSchema","fields":[{"name":"field1","type":"string"}],"excludeOnFetch":false}""",
                });
            else
                await route.ContinueAsync();
        });

        await _create.SchemaNameInput.FillAsync("TestSchema");
        await _create.FieldNameInput(1).FillAsync("field1");
        await _create.SubmitButton.ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/schemas$"),
            new() { Timeout = 10_000 });
    }
}
