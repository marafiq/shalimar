using Microsoft.Playwright;
using Xunit;

namespace Shalimar.SandboxApp.Tests;

[Collection("Sandbox")]
[Trait("Category", "Integration")]
public class UiTests(SandboxAppFixture fixture)
{
    private static async Task WaitForReactAsync(IPage page)
    {
        await page.WaitForFunctionAsync("() => { const el = document.getElementById('root'); return !!el && el.childElementCount > 0; }");
    }

    private static bool ShouldIgnoreConsoleError(string text)
    {
        // Chromium reports fetch/XHR 400s as console errors ("Failed to load resource...").
        // In our TDD flows, we intentionally trigger 400 ValidationProblem responses.
        if (text.Contains("Failed to load resource", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("status of 400", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    [Fact]
    public async Task Dashboard_Widgets_Open_Modals()
    {
        await fixture.ResetAsync();
        var (context, page) = await fixture.NewPageAsync();
        var capture = true;
        var errors = new List<string>();
        page.Console += (_, m) =>
        {
            if (!capture) return;
            if (m.Type != "error") return;
            if (ShouldIgnoreConsoleError(m.Text)) return;
            errors.Add($"console: {m.Text}");
        };
        page.PageError += (_, e) => { if (capture) errors.Add($"pageerror: {e}"); };
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/dashboard");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("dash-incidents").ClickAsync();
            await page.GetByTestId("dash-modal").WaitForAsync();
            await page.GetByTestId("dash-modal-title").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

            await page.GetByTestId("dash-residents").ClickAsync();
            await page.GetByTestId("dash-modal").WaitForAsync();
            await page.GetByTestId("dash-modal-title").WaitForAsync();
        }
        finally
        {
            capture = false;
            if (errors.Count > 0)
                throw new Exception(string.Join("\n", errors));
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Residents_Create_Shows_Validation_Then_Succeeds()
    {
        await fixture.ResetAsync();
        var (context, page) = await fixture.NewPageAsync();
        var capture = true;
        var errors = new List<string>();
        page.Console += (_, m) =>
        {
            if (!capture) return;
            if (m.Type != "error") return;
            if (ShouldIgnoreConsoleError(m.Text)) return;
            errors.Add($"console: {m.Text}");
        };
        page.PageError += (_, e) => { if (capture) errors.Add($"pageerror: {e}"); };
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/residents?pane=new");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("resident-save").ClickAsync();
            await page.GetByText("'Name' must not be empty.", new() { Exact = true }).WaitForAsync();

            await page.GetByLabel("Name").FillAsync("Robert Green");
            await page.GetByLabel("Room").FillAsync("C-105");
            await page.GetByLabel("Care level").FillAsync("Assisted Living");

            await page.GetByTestId("resident-save").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.GetByTestId("resident-row").GetByText("Robert Green").WaitForAsync();
        }
        finally
        {
            capture = false;
            if (errors.Count > 0)
                throw new Exception(string.Join("\n", errors));
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Incidents_Create_Shows_Validation_Then_Succeeds()
    {
        await fixture.ResetAsync();
        var (context, page) = await fixture.NewPageAsync();
        var capture = true;
        var errors = new List<string>();
        page.Console += (_, m) =>
        {
            if (!capture) return;
            if (m.Type != "error") return;
            if (ShouldIgnoreConsoleError(m.Text)) return;
            errors.Add($"console: {m.Text}");
        };
        page.PageError += (_, e) => { if (capture) errors.Add($"pageerror: {e}"); };
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/incidents?pane=new");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("incident-save").ClickAsync();
            await page.GetByText("'Kind' must not be empty.", new() { Exact = true }).WaitForAsync();

            await page.GetByLabel("Kind").FillAsync("Fall");
            await page.GetByLabel("Summary").FillAsync("Unwitnessed fall; vitals stable.");
            await page.GetByLabel("Status").FillAsync("open");
            await page.GetByLabel("Resident id").FillAsync("r_1");

            await page.GetByTestId("incident-save").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("incident-row").GetByText("Fall").First.WaitForAsync();
        }
        finally
        {
            capture = false;
            if (errors.Count > 0)
                throw new Exception(string.Join("\n", errors));
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Screenshots_Three_Core_Pages()
    {
        await fixture.ResetAsync();
        var (context, page) = await fixture.NewPageAsync();
        var capture = true;
        var errors = new List<string>();
        page.Console += (_, m) =>
        {
            if (!capture) return;
            if (m.Type != "error") return;
            if (ShouldIgnoreConsoleError(m.Text)) return;
            errors.Add($"console: {m.Text}");
        };
        page.PageError += (_, e) => { if (capture) errors.Add($"pageerror: {e}"); };
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/dashboard");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = Path.Combine("Snapshots", "seniorliving-dashboard.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/residents");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = Path.Combine("Snapshots", "seniorliving-residents.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/incidents");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = Path.Combine("Snapshots", "seniorliving-incidents.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/dashboard");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.GetByTestId("dash-residents").ClickAsync();
            await page.GetByTestId("dash-modal-title").WaitForAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine("Snapshots", "seniorliving-dashboard-modal.png"), FullPage = true });
        }
        finally
        {
            capture = false;
            if (errors.Count > 0)
                throw new Exception(string.Join("\n", errors));
            await context.DisposeAsync();
        }
    }
}

