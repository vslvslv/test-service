using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class SidebarPage(IPage page)
{
    public async Task GotoAsync() => await page.GotoAsync("/");

    // ── Header controls ──────────────────────────────────────────────────────
    // aria-label toggles between these two states on the same button.
    public ILocator CollapseButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Collapse navigation" });
    public ILocator ExpandButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Expand navigation" });

    public ILocator SearchInput =>
        page.GetByPlaceholder(new System.Text.RegularExpressions.Regex("Search entities"));

    // The workspace badge in the header shows the active route's label (e.g. "Schemas").
    // Scoped to <header> to avoid matching the same text in the sidebar nav links.
    public ILocator WorkspaceBadgeText(string label) =>
        page.Locator("header").GetByText(label);

    // ── Sidebar brand ────────────────────────────────────────────────────────
    // Scoped to <aside> to avoid collisions with page-level h1 elements.
    public ILocator BrandName =>
        page.Locator("aside").GetByRole(AriaRole.Heading, new() { Name = "Test Service" });

    // ── Nav links ────────────────────────────────────────────────────────────
    public ILocator DashboardLink => page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" });
    public ILocator EntitiesLink => page.GetByRole(AriaRole.Link, new() { Name = "Entities" });
    public ILocator SchemasLink => page.GetByRole(AriaRole.Link, new() { Name = "Schemas" });
    public ILocator EnvironmentsLink => page.GetByRole(AriaRole.Link, new() { Name = "Environments" });
    public ILocator ActivityLink => page.GetByRole(AriaRole.Link, new() { Name = "Activity" });
    public ILocator UsersLink => page.GetByRole(AriaRole.Link, new() { Name = "Users" });
    public ILocator MocksLink => page.GetByRole(AriaRole.Link, new() { Name = "Mocks" });
    public ILocator SettingsLink => page.GetByRole(AriaRole.Link, new() { Name = "Settings" });

    // ── User info ────────────────────────────────────────────────────────────
    public ILocator UsernameLabel =>
        page.Locator("aside").GetByText(TestConfig.Username, new() { Exact = true });
    public ILocator RoleLabel =>
        page.Locator("aside p").GetByText(
            new System.Text.RegularExpressions.Regex(
                "^(Admin|Contributor)$",
                System.Text.RegularExpressions.RegexOptions.None));

    // ── Logout ───────────────────────────────────────────────────────────────
    public ILocator LogoutButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Logout" });

    // ── Search dropdown ──────────────────────────────────────────────────────
    public ILocator QuickAccessLabel => page.GetByText("Quick Access");
    public ILocator SearchNoResultsMessage =>
        page.GetByText(
            new System.Text.RegularExpressions.Regex(
                "No results found",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));
}
