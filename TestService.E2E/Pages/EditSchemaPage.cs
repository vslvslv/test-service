using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class EditSchemaPage(IPage page)
{
    public async Task GotoAsync(string schemaName)
    {
        await page.GotoAsync($"/schemas/{schemaName}");
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".animate-spin.h-12"))
            .ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }

    // ── Loading ────────────────────────────────────────────────────────────────
    public ILocator Spinner => page.Locator(".animate-spin.h-12");

    // ── Schema metadata ────────────────────────────────────────────────────────
    // Schema name heading in the hero section.
    public ILocator SchemaNameHeading => page.Locator("main h1");
    // Disabled schema name input — immutable after creation.
    public ILocator SchemaNameInput => page.Locator("input[disabled]");
    public ILocator AutoConsumeCheckbox =>
        page.GetByLabel("Auto-consume on fetch");

    // ── Field rows ─────────────────────────────────────────────────────────────
    // "Field N" text is 4 DOM levels below the field row container div.
    private ILocator FieldRow(int oneBasedIndex) =>
        page.GetByText($"Field {oneBasedIndex}", new() { Exact = true })
            .Locator("xpath=../../../..");

    public ILocator FieldNameInput(int index) =>
        FieldRow(index).GetByPlaceholder("e.g. username");
    public ILocator FieldTypeSelect(int index) =>
        FieldRow(index).Locator("select");
    public ILocator FieldDefaultValueInput(int index) =>
        FieldRow(index).GetByPlaceholder("Optional default value");
    public ILocator FieldDescriptionInput(int index) =>
        FieldRow(index).GetByPlaceholder("Optional field description");
    public ILocator FieldRequiredCheckbox(int index) =>
        FieldRow(index).GetByRole(AriaRole.Checkbox, new() { Name = "Required field" });
    public ILocator FieldRemoveButton(int index) =>
        FieldRow(index).GetByTitle("Remove field");

    // ── Toolbar ────────────────────────────────────────────────────────────────
    public ILocator AddFieldButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Add Field" });

    // ── Header actions ─────────────────────────────────────────────────────────
    public ILocator DeleteSchemaButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Delete Schema" });

    // ── Footer actions ─────────────────────────────────────────────────────────
    public ILocator CancelButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
    public ILocator SaveButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Save Changes" });

    // ── Error banner ───────────────────────────────────────────────────────────
    public ILocator ErrorBanner =>
        page.Locator("[class*='border-red-500/40']");

    // ── Not-found state ────────────────────────────────────────────────────────
    public ILocator NotFoundHeading =>
        page.GetByRole(AriaRole.Heading, new() { Name = "Schema not found" });
    public ILocator BackToSchemasButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Back to Schemas" });
}
