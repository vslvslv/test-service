using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class SchemasPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/schemas");
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".animate-spin"))
            .ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }

    // ── Loading ──────────────────────────────────────────────────────────────
    public ILocator Spinner => page.Locator(".animate-spin");

    // ── Toolbar ──────────────────────────────────────────────────────────────
    public ILocator SearchInput =>
        page.GetByPlaceholder("Search schemas by entity name");
    public ILocator CreateButton =>
        page.GetByRole(AriaRole.Button, new() {
            NameRegex = new System.Text.RegularExpressions.Regex(
                "create schema", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        });
    public ILocator SortSelect => page.Locator("select");
    public ILocator ListViewButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "List" });
    public ILocator GridViewButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Grid" });
    public ILocator AutoConsumeFilterCheckbox =>
        page.GetByLabel("Show auto-consume schemas only");

    // ── Summary stats labels ─────────────────────────────────────────────────
    public ILocator TotalSchemasLabel => page.GetByText("Total schemas");
    public ILocator AutoConsumeEnabledLabel => page.GetByText("Auto-consume enabled");
    public ILocator TotalFieldsTrackedLabel => page.GetByText("Total fields tracked");

    // ── Schema list items ────────────────────────────────────────────────────
    // Schema names appear inside <h3> elements in both list and grid view.
    public ILocator SchemaNameHeading(string name) => page.Locator("h3").GetByText(name);
    // The main row button is the one that contains an <h3> (action buttons do not).
    public ILocator SchemaRowButton(string name) =>
        page.GetByRole(AriaRole.Button)
            .Filter(new() { Has = page.Locator("h3").GetByText(name) });
    // Action buttons — unique when a single schema is shown by the mock.
    public ILocator EditButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Edit" });
    public ILocator DeleteButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true });
    public ILocator DeleteEntitiesButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Delete Entities" });

    // ── Error banner ─────────────────────────────────────────────────────────
    public ILocator ErrorBanner =>
        page.Locator("[class*='border-red-500/40']");

    // ── Empty states ─────────────────────────────────────────────────────────
    public ILocator EmptyStateNoSchemasYet => page.GetByText("No schemas yet");
    public ILocator EmptyStateNoMatchMessage =>
        page.GetByText("No schemas match the current filters");
}
