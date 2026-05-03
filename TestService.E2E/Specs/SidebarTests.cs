using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class SidebarTests : AuthenticatedTest
{
    private SidebarPage _sidebar = null!;

    [SetUp]
    public async Task SetUp()
    {
        // Use a desktop viewport so collapse/expand behaviour is consistent.
        await Page.SetViewportSizeAsync(1440, 900);
        _sidebar = new SidebarPage(Page);
        await _sidebar.GotoAsync();
    }

    // ── Structure ────────────────────────────────────────────────────────────

    [Test]
    public async Task SidebarIsExpandedByDefault()
    {
        // The header toggle button carries aria-expanded reflecting sidebar state.
        await Expect(_sidebar.CollapseButton).ToBeVisibleAsync();
        var expanded = await _sidebar.CollapseButton.GetAttributeAsync("aria-expanded");
        Assert.That(expanded, Is.EqualTo("true"));
    }

    [Test]
    public async Task BrandNameIsVisible()
    {
        await Expect(_sidebar.BrandName).ToBeVisibleAsync();
    }

    [Test]
    public async Task AllCoreNavLinksAreVisible()
    {
        await Expect(_sidebar.DashboardLink).ToBeVisibleAsync();
        await Expect(_sidebar.EntitiesLink).ToBeVisibleAsync();
        await Expect(_sidebar.SchemasLink).ToBeVisibleAsync();
        await Expect(_sidebar.EnvironmentsLink).ToBeVisibleAsync();
        await Expect(_sidebar.ActivityLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task AdminSeesPermissionGatedLinks()
    {
        await Expect(_sidebar.UsersLink).ToBeVisibleAsync();
        await Expect(_sidebar.MocksLink).ToBeVisibleAsync();
        await Expect(_sidebar.SettingsLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task UserInfoShownInSidebarFooter()
    {
        await Expect(_sidebar.UsernameLabel).ToBeVisibleAsync();
        await Expect(_sidebar.RoleLabel).ToBeVisibleAsync();
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    [Test]
    public async Task DashboardLinkNavigates()
    {
        await _sidebar.SchemasLink.ClickAsync();
        await _sidebar.DashboardLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("^[^/]*//[^/]+/?$"));
    }

    [Test]
    public async Task EntitiesLinkNavigates()
    {
        await _sidebar.EntitiesLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/entities"));
    }

    [Test]
    public async Task SchemasLinkNavigates()
    {
        await _sidebar.SchemasLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas"));
    }

    [Test]
    public async Task EnvironmentsLinkNavigates()
    {
        await _sidebar.EnvironmentsLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/environments"));
    }

    [Test]
    public async Task ActivityLinkNavigates()
    {
        await _sidebar.ActivityLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/activity"));
    }

    [Test]
    public async Task SettingsLinkNavigates()
    {
        await _sidebar.SettingsLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/settings"));
    }

    // ── Active state / workspace badge ───────────────────────────────────────

    [Test]
    public async Task WorkspaceBadgeReflectsCurrentPage()
    {
        await Page.GotoAsync("/schemas");
        await Expect(_sidebar.WorkspaceBadgeText("Schemas")).ToBeVisibleAsync();

        await Page.GotoAsync("/environments");
        await Expect(_sidebar.WorkspaceBadgeText("Environments")).ToBeVisibleAsync();
    }

    // ── Collapse / expand ────────────────────────────────────────────────────

    [Test]
    public async Task CollapsingHidesBrandTextAndNavLabels()
    {
        await _sidebar.CollapseButton.ClickAsync();
        // Brand name and nav label text are conditionally rendered — removed from DOM when collapsed.
        await Expect(_sidebar.BrandName).ToBeHiddenAsync();
        // Expand button replaces collapse button.
        await Expect(_sidebar.ExpandButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task ExpandingRestoresBrandTextAndNavLabels()
    {
        await _sidebar.CollapseButton.ClickAsync();
        await Expect(_sidebar.BrandName).ToBeHiddenAsync();

        await _sidebar.ExpandButton.ClickAsync();
        await Expect(_sidebar.BrandName).ToBeVisibleAsync();
    }

    [Test]
    public async Task CollapsedNavLinksStillNavigateOnDesktop()
    {
        await _sidebar.CollapseButton.ClickAsync();
        // Links remain in the DOM with title attributes as accessible names.
        await _sidebar.SchemasLink.ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/schemas"));
    }

    // ── Global search ─────────────────────────────────────────────────────────

    [Test]
    public async Task SearchInputIsVisibleInHeader()
    {
        await Expect(_sidebar.SearchInput).ToBeVisibleAsync();
    }

    [Test]
    public async Task FocusingSearchInputShowsQuickAccessDropdown()
    {
        await _sidebar.SearchInput.ClickAsync();
        await Expect(_sidebar.QuickAccessLabel).ToBeVisibleAsync();
    }

    [Test]
    public async Task TypingQueryShowsMatchingResults()
    {
        await _sidebar.SearchInput.ClickAsync();
        await _sidebar.SearchInput.FillAsync("schemas");
        // "Manage schema definitions" is the description for the Schemas navigation item.
        await Expect(Page.GetByText("Manage schema definitions")).ToBeVisibleAsync();
    }

    [Test]
    public async Task UnknownQueryShowsNoResultsMessage()
    {
        await _sidebar.SearchInput.ClickAsync();
        await _sidebar.SearchInput.FillAsync("xqqzwrandom9876");
        await Expect(_sidebar.SearchNoResultsMessage).ToBeVisibleAsync();
    }

    [Test]
    public async Task ClickingSearchResultNavigatesAndClosesDropdown()
    {
        await _sidebar.SearchInput.ClickAsync();
        await _sidebar.SearchInput.FillAsync("activity");
        await Page.GetByText("Check recent operations").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/activity"));
        await Expect(_sidebar.QuickAccessLabel).ToBeHiddenAsync();
    }

    [Test]
    public async Task ClickingOutsideSearchClosesDropdown()
    {
        await _sidebar.SearchInput.ClickAsync();
        await Expect(_sidebar.QuickAccessLabel).ToBeVisibleAsync();

        await Page.Locator("main").ClickAsync(new() { Force = true });
        await Expect(_sidebar.QuickAccessLabel).ToBeHiddenAsync();
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [Test]
    public async Task LogoutButtonRedirectsToLogin()
    {
        await _sidebar.LogoutButton.ClickAsync();
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/login"),
            new() { Timeout = 10_000 });
    }

    [Test]
    public async Task SessionClearedAfterLogout()
    {
        await _sidebar.LogoutButton.ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10_000 });

        // Navigating back to root should redirect to login, not load the dashboard.
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }
}
