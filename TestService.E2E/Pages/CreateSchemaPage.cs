using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class CreateSchemaPage(IPage page)
{
    public async Task GotoAsync() => await page.GotoAsync("/schemas/new");

    // ── Page heading ──────────────────────────────────────────────────────────
    public ILocator Heading =>
        page.GetByRole(AriaRole.Heading, new() { Name = "Design a new entity contract" });

    // ── Schema details ─────────────────────────────────────────────────────────
    public ILocator SchemaNameInput =>
        page.GetByPlaceholder("e.g. user-account, test-agent");
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
    public ILocator FieldRequiredCheckbox(int index) =>
        FieldRow(index).GetByRole(AriaRole.Checkbox, new() { Name = "Required field" });
    public ILocator FieldUniqueCheckbox(int index) =>
        FieldRow(index).GetByRole(AriaRole.Checkbox, new() { Name = "Unique field" });
    public ILocator FieldRemoveButton(int index) =>
        FieldRow(index).GetByTitle("Remove field");

    // ── Toolbar ────────────────────────────────────────────────────────────────
    public ILocator AddFieldButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Add Field" });

    // ── Footer actions ─────────────────────────────────────────────────────────
    public ILocator CancelButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
    public ILocator SubmitButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Create Schema" });

    // ── Error banner ───────────────────────────────────────────────────────────
    public ILocator ErrorBanner =>
        page.Locator("[class*='border-red-500/40']");
}
