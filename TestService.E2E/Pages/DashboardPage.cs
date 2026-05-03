using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class DashboardPage(IPage page)
{
    // Stat cards live inside the section-grid to disambiguate from similarly
    // worded text elsewhere (e.g. "Environments" vs "Manage Environments").
    private ILocator StatCardsGrid => page.Locator(".section-grid");

    public async Task GotoAsync()
    {
        await page.GotoAsync("/");
        // Wait for initial data load — dashboard swaps entire content with spinner while isLoading=true.
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".animate-spin.h-12"))
            .ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }

    // ── Hero ─────────────────────────────────────────────────────────────────
    public ILocator Heading =>
        page.GetByRole(AriaRole.Heading, new() { NameRegex = new System.Text.RegularExpressions.Regex("Welcome back") });
    public ILocator OperationsOverviewLabel => page.GetByText("Operations Overview");
    public ILocator CurrentBalancePanel => page.GetByText("Current Balance");

    // ── Stat cards ───────────────────────────────────────────────────────────
    public ILocator TotalSchemasCard =>
        StatCardsGrid.GetByRole(AriaRole.Button).Filter(new() { HasText = "Total Schemas" });
    public ILocator EnvironmentsCard =>
        StatCardsGrid.GetByRole(AriaRole.Button).Filter(new() { HasText = "Environments" });
    public ILocator AvailableEntitiesCard =>
        StatCardsGrid.GetByRole(AriaRole.Button).Filter(new() { HasText = "Available Entities" });
    public ILocator ConsumedEntitiesCard =>
        StatCardsGrid.GetByRole(AriaRole.Button).Filter(new() { HasText = "Consumed Entities" });

    // ── Recent Schemas section ───────────────────────────────────────────────
    public ILocator RecentSchemasHeading =>
        page.GetByRole(AriaRole.Heading, new() { NameRegex = new System.Text.RegularExpressions.Regex("Recent Schemas") });
    public ILocator ViewAllButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "View All" });
    // Schema rows are the only buttons on the page that contain an h3.
    public ILocator FirstSchemaRow =>
        page.GetByRole(AriaRole.Button).Filter(new() { Has = page.Locator("h3") }).First;
    public ILocator EmptyStateSchemasMessage =>
        page.GetByText("No schemas found");
    public ILocator CreateFirstSchemaButton =>
        page.GetByRole(AriaRole.Button, new() {
            NameRegex = new System.Text.RegularExpressions.Regex("Create your first schema",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        });

    // ── Quick Actions ────────────────────────────────────────────────────────
    public ILocator QuickActionsHeading =>
        page.GetByRole(AriaRole.Heading, new() { Name = "Quick Actions" });
    public ILocator CreateNewSchemaAction =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Create New Schema" });
    public ILocator ManageEnvironmentsAction =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Manage Environments" });
    public ILocator ViewActivityAction =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "View Activity" });

    // ── System Status ────────────────────────────────────────────────────────
    public ILocator SystemStatusHeading =>
        page.GetByRole(AriaRole.Heading, new() { Name = "System Status" });
    public ILocator ApiServiceStatus => page.GetByText("Operational", new() { Exact = true });
    public ILocator DatabaseStatus => page.GetByText("Connected", new() { Exact = true });
    public ILocator MessageBusStatus => page.GetByText("Active", new() { Exact = true });

    // ── Loading ──────────────────────────────────────────────────────────────
    public ILocator Spinner => page.Locator(".animate-spin.h-12");
}
