using Microsoft.Playwright;
using Xunit;

namespace Shalimar.SandboxApp.Tests;

[Collection("Sandbox")]
[Trait("Category", "Integration")]
public class UiTests(SandboxAppFixture fixture)
{
    private static string SnapshotPath(string filename)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var dir = Path.Combine(repoRoot, "tests", "Shalimar.SandboxApp.Tests", "Snapshots");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, filename);
    }

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
    public async Task Observations_Create_Shows_Validation_Then_Succeeds()
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
            await page.GotoAsync($"{fixture.BaseUrl}/observations?pane=new");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("observation-save").ClickAsync();
            await page.GetByText("'Kind' must not be empty.", new() { Exact = true }).WaitForAsync();

            await page.GetByLabel("Kind").FillAsync("Vitals");
            await page.GetByLabel("Note").FillAsync("BP slightly elevated; monitoring.");
            await page.GetByLabel("Resident id").FillAsync("r_3");

            await page.GetByTestId("observation-save").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("observation-row").GetByText("Vitals").First.WaitForAsync();
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
    public async Task MedPassSchedule_Create_Shows_Validation_Then_Succeeds()
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
            await page.GotoAsync($"{fixture.BaseUrl}/medpass/schedule?pane=new");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("schedule-save").ClickAsync();
            await page.GetByText("'Resident Id' must not be empty.", new() { Exact = true }).WaitForAsync();

            await page.GetByLabel("Resident id").FillAsync("r_1");
            await page.GetByLabel("Med id").FillAsync("m_3");
            await page.GetByLabel("Time (HH:mm)").FillAsync("14:00");
            await page.GetByLabel("Frequency").FillAsync("prn");

            await page.GetByTestId("schedule-save").ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("schedule-row").GetByText("14:00").First.WaitForAsync();
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
    public async Task PassMeds_Can_Record_Pass()
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
            await page.GotoAsync($"{fixture.BaseUrl}/medpass/pass");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByTestId("pass-given").First.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);

            await page.GetByText("Outcome:", new() { Exact = false }).First.WaitForAsync();
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
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-dashboard.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/residents");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-residents.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/incidents");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-incidents.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/observations");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-observations.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/medpass/schedule");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-medpass-schedule.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/medpass/pass");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-pass-meds.png"), FullPage = true });

            await page.GotoAsync($"{fixture.BaseUrl}/dashboard");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await WaitForReactAsync(page);
            await page.GetByTestId("dash-residents").ClickAsync();
            await page.GetByTestId("dash-modal-title").WaitForAsync();
            await page.ScreenshotAsync(new() { Path = SnapshotPath("seniorliving-dashboard-modal.png"), FullPage = true });
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

