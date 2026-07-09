using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class MocksPage(IPage page)
{
    public async Task GotoAsync()
    {
        await page.GotoAsync("/mocks");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // ── Hero ─────────────────────────────────────────────────────────────────
    public ILocator Heading => page.Locator("h1").First;
    public ILocator EyebrowLabel => page.GetByText("Mock Operations Console");
    public ILocator RefreshButton => page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });
    public ILocator NewExpectationButton => page.GetByRole(AriaRole.Button, new() { Name = "New Expectation" });

    // ── Tabs ─────────────────────────────────────────────────────────────────
    // Each tab button contains two spans: an eyebrow ("Routing Rules" / "Traffic Review"
    // / "Assertion Runner") and a label ("Expectations" / "Request Logs" / "Verify").
    // Filter by the eyebrow — it is unique on the page and stable.
    public ILocator ExpectationsTab =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Routing Rules" });
    public ILocator LogsTab =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Traffic Review" });
    public ILocator VerifyTab =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Assertion Runner" });

    public ILocator ActiveTabContentHeading(string label) =>
        page.GetByRole(AriaRole.Heading, new() { Name = label, Exact = true });

    // ── Create / Edit dialog ─────────────────────────────────────────────────
    public ILocator ExpectationDialog =>
        page.GetByRole(AriaRole.Dialog).Filter(new() { Has = page.Locator("#mock-expectation-dialog-title") });
    public ILocator CreateDialogTitle =>
        page.Locator("#mock-expectation-dialog-title").Filter(new() { HasText = "Create Expectation" });
    public ILocator EditDialogTitle =>
        page.Locator("#mock-expectation-dialog-title").Filter(new() { HasText = "Edit Expectation" });
    public ILocator CloseDialogButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Close expectation dialog" });
}
