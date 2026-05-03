using Microsoft.Playwright.NUnit;
using TestService.E2E.Pages;

namespace TestService.E2E.Specs;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AuthTests : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = TestConfig.BaseUrl,
        StorageState = "{\"cookies\":[],\"origins\":[]}",
    };

    // ── Form structure ──────────────────────────────────────────────────────

    [Test]
    public async Task RendersSignInForm()
    {
        await Page.GotoAsync("/login");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in" })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Username")).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Password")).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task PasswordInputObscuresText()
    {
        await Page.GotoAsync("/login");
        var inputType = await Page.GetByLabel("Password").GetAttributeAsync("type");
        Assert.That(inputType, Is.EqualTo("password"));
    }

    [Test]
    public async Task UsernameInputReceivesAutoFocusOnLoad()
    {
        await Page.GotoAsync("/login");
        await Expect(Page.GetByLabel("Username")).ToBeFocusedAsync();
    }

    // ── Client-side validation ───────────────────────────────────────────────

    [Test]
    public async Task PreventsSubmissionWithEmptyCredentials()
    {
        await Page.GotoAsync("/login");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhitespaceOnlyCredentialsDoNotAuthenticate()
    {
        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync("   ");
        await Page.GetByLabel("Password").FillAsync("   ");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/login"),
            new() { Timeout = 3_000 });
    }

    [Test]
    public async Task SubmitButtonIsDisabledDuringAuthentication()
    {
        await Page.RouteAsync("**/api/auth/login", async route =>
        {
            await Task.Delay(1500);
            await route.ContinueAsync();
        });

        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.UsernameInput.FillAsync(TestConfig.Username);
        await login.PasswordInput.FillAsync(TestConfig.Password);
        await login.SubmitButton.ClickAsync();

        await Expect(login.LoadingButton).ToBeVisibleAsync();
        await Expect(login.LoadingButton).ToBeDisabledAsync();

        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });
    }

    // ── Credential handling ──────────────────────────────────────────────────

    [Test]
    public async Task TrimsWhitespaceAroundCredentials()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.UsernameInput.FillAsync($" {TestConfig.Username} ");
        await login.PasswordInput.FillAsync($" {TestConfig.Password} ");
        await login.SubmitButton.ClickAsync();
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    [Test]
    public async Task SubmitsFormOnEnterKeyInPasswordField()
    {
        await Page.GotoAsync("/login");
        await Page.GetByLabel("Username").FillAsync(TestConfig.Username);
        await Page.GetByLabel("Password").FillAsync(TestConfig.Password);
        await Page.GetByLabel("Password").PressAsync("Enter");
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    // ── Error messages ───────────────────────────────────────────────────────

    [Test]
    public async Task ShowsErrorOnWrongCredentials()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync("notauser", "wrongpass");
        await Expect(login.ErrorBanner).ToBeVisibleAsync();
    }

    [Test]
    public async Task ShowsCorrectErrorMessageForUnknownUser()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync("nonexistent_user_xyz_abc", "SomePassword@123");
        await Expect(Page.GetByText("Invalid username or password.")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ShowsCorrectErrorMessageForWrongPassword()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync(TestConfig.Username, "WrongPassword@123");
        await Expect(Page.GetByText("Invalid or incorrect password.")).ToBeVisibleAsync();
    }

    // ── Navigation and session ───────────────────────────────────────────────

    [Test]
    public async Task RedirectsToDashboardAfterSuccessfulLogin()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync(TestConfig.Username, TestConfig.Password);
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    [Test]
    public async Task RedirectsAuthenticatedUsersAwayFromLogin()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync(TestConfig.Username, TestConfig.Password);
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));

        await Page.GotoAsync("/login");
        await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
    }

    [Test]
    public async Task SessionPersistsAfterPageReload()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync(TestConfig.Username, TestConfig.Password);
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });

        await Page.ReloadAsync();

        await Expect(Page).Not.ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/login"),
            new() { Timeout = 10_000 });
    }

    // ── Unauthenticated redirect ─────────────────────────────────────────────

    [Test]
    public async Task UnauthenticatedUser_NavigatingToDashboard_RedirectsToLogin()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/login"),
            new() { Timeout = 5_000 });
    }

    [Test]
    public async Task UnauthenticatedUser_NavigatingToSchemas_RedirectsToLogin()
    {
        await Page.GotoAsync("/schemas");
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex("/login"),
            new() { Timeout = 5_000 });
    }

    // ── Error recovery ───────────────────────────────────────────────────────

    [Test]
    public async Task FailedLogin_FollowedByCorrectCredentials_Succeeds()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();

        await login.LoginAsync("nonexistent_xyz", "WrongPass@1");
        await Expect(login.ErrorBanner).ToBeVisibleAsync();

        await login.UsernameInput.FillAsync(TestConfig.Username);
        await login.PasswordInput.FillAsync(TestConfig.Password);
        await login.SubmitButton.ClickAsync();

        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });
    }

    // ── Post-login session state ─────────────────────────────────────────────

    [Test]
    public async Task TokenPersistedToLocalStorage_AfterSuccessfulLogin()
    {
        var login = new LoginPage(Page);
        await login.GotoAsync();
        await login.LoginAsync(TestConfig.Username, TestConfig.Password);
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });

        var token = await Page.EvaluateAsync<string?>("() => localStorage.getItem('token')");
        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    // ── Role-based login ─────────────────────────────────────────────────────

    [Test]
    public async Task ContributorUser_CanLoginAndAccessDashboard()
    {
        var username = $"e2e_contrib_{Guid.NewGuid():N}"[..24];
        const string password = "Contrib@E2E1";
        string? userId = null;

        await using var api = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = TestConfig.ApiUrl });

        var loginRes = await api.PostAsync("/api/auth/login", new()
        {
            DataObject = new { username = TestConfig.Username, password = TestConfig.Password }
        });
        var loginJson = await loginRes.JsonAsync();
        var adminToken = loginJson!.Value.GetProperty("token").GetString()!;

        var createRes = await api.PostAsync("/api/users", new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
            DataObject = new { username, email = $"{username}@e2e.test", password, role = 0 }
        });
        var createdJson = await createRes.JsonAsync();
        userId = createdJson!.Value.GetProperty("id").GetString();

        try
        {
            var login = new LoginPage(Page);
            await login.GotoAsync();
            await login.LoginAsync(username, password);
            await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10_000 });
            await Expect(Page).Not.ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        }
        finally
        {
            if (userId != null)
                await api.DeleteAsync($"/api/users/{userId}", new()
                {
                    Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" }
                });
        }
    }

    // ── Inactive user ────────────────────────────────────────────────────────

    [Test]
    public async Task InactiveUser_ShowsGenericInvalidCredentialsError()
    {
        var username = $"e2e_inactive_{Guid.NewGuid():N}"[..25];
        const string password = "Inactive@E2E1";
        string? userId = null;
        string adminToken = string.Empty;

        await using var api = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = TestConfig.ApiUrl });

        var loginRes = await api.PostAsync("/api/auth/login", new()
        {
            DataObject = new { username = TestConfig.Username, password = TestConfig.Password }
        });
        var loginJson = await loginRes.JsonAsync();
        adminToken = loginJson!.Value.GetProperty("token").GetString()!;

        var createRes = await api.PostAsync("/api/users", new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
            DataObject = new { username, email = $"{username}@e2e.test", password, role = 0 }
        });
        var createdJson = await createRes.JsonAsync();
        userId = createdJson!.Value.GetProperty("id").GetString();

        await api.PutAsync($"/api/users/{userId}", new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
            DataObject = new { isActive = false }
        });

        try
        {
            var login = new LoginPage(Page);
            await login.GotoAsync();
            await login.LoginAsync(username, password);
            await Expect(Page.GetByText("Invalid username or password.")).ToBeVisibleAsync();
        }
        finally
        {
            if (userId != null)
                await api.DeleteAsync($"/api/users/{userId}", new()
                {
                    Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" }
                });
        }
    }
}
