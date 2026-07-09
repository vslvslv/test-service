using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class MocksTests : AuthenticatedTest
{
    private MocksPage _mocks = null!;

    [SetUp]
    public async Task SetUp()
    {
        _mocks = new MocksPage(Page);
        await _mocks.GotoAsync();
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task RedirectsToLoginWhenUnauthenticated()
    {
        var context = await Browser.NewContextAsync(new()
        {
            BaseURL = TestConfig.BaseUrl,
            StorageState = "{\"cookies\":[],\"origins\":[]}",
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/mocks");
        await Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await context.CloseAsync();
    }

    // ── Render ───────────────────────────────────────────────────────────────

    [Test]
    public async Task ShowsHeadingAndEyebrow()
    {
        await Expect(_mocks.Heading).ToBeVisibleAsync();
        await Expect(_mocks.EyebrowLabel).ToBeVisibleAsync();
    }

    [Test]
    public async Task ShowsRefreshAndNewExpectationButtons()
    {
        await Expect(_mocks.RefreshButton).ToBeVisibleAsync();
        await Expect(_mocks.NewExpectationButton).ToBeVisibleAsync();
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task RendersThreeTabs()
    {
        await Expect(_mocks.ExpectationsTab).ToBeVisibleAsync();
        await Expect(_mocks.LogsTab).ToBeVisibleAsync();
        await Expect(_mocks.VerifyTab).ToBeVisibleAsync();
    }

    [Test]
    public async Task SwitchingToLogsTab_HidesNewExpectationButton()
    {
        // The "New Expectation" button is gated behind activeTab === 'expectations'.
        await _mocks.LogsTab.ClickAsync();
        await Expect(_mocks.NewExpectationButton).ToBeHiddenAsync();
    }

    [Test]
    public async Task SwitchingToVerifyAndBack_KeepsHeadingVisible()
    {
        await _mocks.VerifyTab.ClickAsync();
        await Expect(_mocks.Heading).ToBeVisibleAsync();
        await _mocks.ExpectationsTab.ClickAsync();
        await Expect(_mocks.NewExpectationButton).ToBeVisibleAsync();
    }

    // ── Create dialog ────────────────────────────────────────────────────────
    // Regression guard: the closeModal hoisting bug previously caused a TDZ
    // error during render that prevented the page (and dialog) from mounting.

    [Test]
    public async Task ClickingNewExpectation_OpensCreateDialog()
    {
        await _mocks.NewExpectationButton.ClickAsync();
        await Expect(_mocks.CreateDialogTitle).ToBeVisibleAsync();
    }

    [Test]
    public async Task DialogCloseButton_DismissesDialog()
    {
        await _mocks.NewExpectationButton.ClickAsync();
        await Expect(_mocks.CreateDialogTitle).ToBeVisibleAsync();

        await _mocks.CloseDialogButton.ClickAsync();
        await Expect(_mocks.CreateDialogTitle).ToBeHiddenAsync();
    }

    [Test]
    public async Task EscapeKey_DismissesDialog()
    {
        // This exercises the very useEffect+closeModal interaction that hit the TDZ.
        // If closeModal is mishoisted again, the dialog will not render at all (page
        // will be blank) and the open assertion will fail before we even reach Escape.
        await _mocks.NewExpectationButton.ClickAsync();
        await Expect(_mocks.CreateDialogTitle).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");
        await Expect(_mocks.CreateDialogTitle).ToBeHiddenAsync();
    }
}
