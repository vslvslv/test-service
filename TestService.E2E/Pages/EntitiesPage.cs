using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class EntitiesPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/entities");
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".animate-spin"))
            .ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }

    // ── Loading ────────────────────────────────────────────────────────────────
    public ILocator Spinner => page.Locator(".animate-spin");

    // ── Hero stat cards ────────────────────────────────────────────────────────
    // Scoped to .stat-card to avoid collision with section eyebrow labels.
    private ILocator StatCards => page.Locator(".stat-card");
    public ILocator EntityTypesStatCard => StatCards.Filter(new() { HasText = "Entity Types" });
    public ILocator AvailableStatCard => StatCards.Filter(new() { HasText = "Available" });
    public ILocator ConsumedStatCard => StatCards.Filter(new() { HasText = "Consumed" });

    // ── Aggregate stats panel ──────────────────────────────────────────────────
    public ILocator TotalEntityRecordsLabel => page.GetByText("Total entity records");
    public ILocator ReadyForAllocationLabel => page.GetByText("Ready for allocation");
    public ILocator ExhaustedOrUsedLabel => page.GetByText("Exhausted or already used");

    // ── Toolbar ────────────────────────────────────────────────────────────────
    public ILocator SearchInput => page.GetByPlaceholder("Search entity types");
    public ILocator AutoConsumeFilterCheckbox =>
        page.GetByLabel("Show auto-consume only");

    // ── Entity type rows ───────────────────────────────────────────────────────
    // Each entity type row is a button containing an <h3> with the entity name.
    public ILocator EntityTypeRow(string name) =>
        page.GetByRole(AriaRole.Button)
            .Filter(new() { Has = page.Locator("h3").GetByText(name) });
    public ILocator EntityTypeHeading(string name) =>
        page.Locator("h3").GetByText(name);

    // ── Empty states ───────────────────────────────────────────────────────────
    public ILocator EmptyStateNoEntityTypes =>
        page.GetByText("No entity types available");
    public ILocator EmptyStateNoMatchMessage =>
        page.GetByText("No entity types match the current filters");

    // ── Error banner ───────────────────────────────────────────────────────────
    public ILocator ErrorBanner => page.Locator("[class*='border-red-500/40']");
}
