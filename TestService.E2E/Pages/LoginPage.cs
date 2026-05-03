using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class LoginPage(IPage page)
{
    private readonly ILocator _usernameInput = page.GetByLabel("Username");
    private readonly ILocator _passwordInput = page.GetByLabel("Password");
    private readonly ILocator _submitButton = page.GetByRole(AriaRole.Button, new() { Name = "Sign In" });
    private readonly ILocator _errorBanner = page.Locator("[role=alert]");
    private readonly ILocator _loadingButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("Signing in", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });

    public async Task GotoAsync() => await page.GotoAsync("/login");

    public async Task LoginAsync(string username, string password)
    {
        await _usernameInput.FillAsync(username);
        await _passwordInput.FillAsync(password);
        await _submitButton.ClickAsync();
    }

    public ILocator ErrorBanner => _errorBanner;
    public ILocator SubmitButton => _submitButton;
    public ILocator LoadingButton => _loadingButton;
    public ILocator UsernameInput => _usernameInput;
    public ILocator PasswordInput => _passwordInput;
}
