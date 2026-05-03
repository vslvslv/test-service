using Microsoft.Playwright;

namespace TestService.E2E.Pages;

public class EntityListPage(IPage page)
{
    public async Task GotoAsync(string entityType)
    {
        await page.GotoAsync($"/entities/{entityType}");
        await Microsoft.Playwright.Assertions.Expect(page.Locator(".animate-spin"))
            .ToHaveCountAsync(0, new() { Timeout = 10_000 });
    }

    // ── Loading ────────────────────────────────────────────────────────────────
    public ILocator Spinner => page.Locator(".animate-spin");

    // ── Page heading ───────────────────────────────────────────────────────────
    public ILocator EntityTypeHeading => page.Locator("main h1");

    // ── Hero stat cards ────────────────────────────────────────────────────────
    // Scoped to .stat-card to avoid collision with row badges.
    private ILocator StatCards => page.Locator(".stat-card");
    public ILocator EntitiesStatCard => StatCards.Filter(new() { HasText = "Entities" });
    public ILocator AvailableStatCard => StatCards.Filter(new() { HasText = "Available" });
    public ILocator ConsumedStatCard => StatCards.Filter(new() { HasText = "Consumed" });
    public ILocator FieldsStatCard => StatCards.Filter(new() { HasText = "Fields" });

    // ── Header action buttons ──────────────────────────────────────────────────
    public ILocator ImportButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Import" }).First;
    public ILocator ExportButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Export" });
    public ILocator CreateEntityButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Create Entity", Exact = true }).First;

    // ── Toolbar ────────────────────────────────────────────────────────────────
    public ILocator SearchInput =>
        page.GetByPlaceholder("Search across entity field values");
    public ILocator ColumnsButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Columns" });
    // Visible only for auto-consume schemas.
    public ILocator ShowConsumedFilterCheckbox =>
        page.GetByRole(AriaRole.Checkbox, new() { Name = "Show consumed entities only" });

    // ── Entity table ───────────────────────────────────────────────────────────
    public ILocator EntityTable => page.Locator("table");
    public ILocator EntityRows => page.Locator("table tbody tr");
    // Per-row action buttons matched by title attribute.
    public ILocator ViewEntityButton => page.GetByTitle("View entity");
    public ILocator DeleteEntityButton => page.GetByTitle("Delete entity");
    public ILocator ResetEntityButton => page.GetByTitle("Reset entity");

    // ── Empty state ────────────────────────────────────────────────────────────
    public ILocator EmptyStateNoEntities => page.GetByText("No entities yet");
    public ILocator EmptyStateNoMatchMessage =>
        page.GetByText("No entities match the current filters");
    public ILocator EmptyStateCreateEntityButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Create Entity", Exact = true }).Nth(1);
    public ILocator EmptyStateImportDatasetButton =>
        page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Import Dataset" });

    // ── Create entity dialog ───────────────────────────────────────────────────
    // All dialog locators are scoped to .modal-shell to avoid collisions with the
    // main page when both are rendered simultaneously.
    private ILocator DialogShell => page.Locator(".modal-shell");
    public ILocator CreateDialogTitle =>
        DialogShell.GetByRole(AriaRole.Heading, new() { Level = 2 });
    public ILocator DialogFieldInput(string fieldName) =>
        DialogShell.GetByPlaceholder($"Enter {fieldName}");
    public ILocator DialogEnvironmentSelect =>
        DialogShell.Locator("label").Filter(new() { HasText = "Environment" }).Locator("select");
    public ILocator DialogCancelButton =>
        DialogShell.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
    public ILocator DialogSubmitButton =>
        DialogShell.GetByRole(AriaRole.Button, new() { Name = "Create Entity", Exact = true });
    public ILocator DialogFieldError(string message) =>
        DialogShell.GetByText(message);
    public ILocator DialogSubmitError =>
        DialogShell.Locator("[class*='border-red-500/40']");

    // ── Import modal ───────────────────────────────────────────────────────────
    public ILocator ImportModalHeading =>
        page.GetByRole(AriaRole.Heading, new() { Name = "Import entities" });
    public ILocator ImportModalCloseButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Close" });

    // ── Not-found state ────────────────────────────────────────────────────────
    public ILocator NotFoundHeading =>
        page.GetByRole(AriaRole.Heading, new() { Name = "Entity type not found" });
    public ILocator BackToEntitiesButton =>
        page.GetByRole(AriaRole.Button, new() { Name = "Back to Entities" });

    // ── Banners ────────────────────────────────────────────────────────────────
    public ILocator ErrorBanner => page.Locator("[class*='border-red-500/40']");
    public ILocator SuccessBanner => page.Locator("[class*='border-emerald-500/40']");
}
