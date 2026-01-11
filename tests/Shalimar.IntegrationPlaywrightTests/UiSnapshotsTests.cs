using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class UiSnapshotsTests(SandboxAppFixture fixture)
{
    [Fact]
    public async Task V2_Workbench()
    {
        await SnapshotRouteAsync(nameof(V2_Workbench), "/v2/workbench", "v2-workbench.png");
    }

    [Fact]
    public async Task V2_Tasks()
    {
        await SnapshotRouteAsync(nameof(V2_Tasks), "/v2/tasks", "v2-tasks.png");
    }

    [Fact]
    public async Task V2_Tasks_Create_Open()
    {
        await SnapshotRouteAsync(nameof(V2_Tasks_Create_Open), "/v2/tasks?create=1", "v2-tasks-create-open.png");
    }

    [Fact]
    public async Task Tasks_Page()
    {
        await SnapshotRouteAsync(nameof(Tasks_Page), "/tasks", "tasks.png");
    }

    [Fact]
    public async Task Tasks_Drawer_New()
    {
        await SnapshotRouteAsync(nameof(Tasks_Drawer_New), "/tasks?drawer=new", "tasks-drawer-new.png");
    }

    [Fact]
    public async Task Tasks_Drawer_Edit_With_Thread()
    {
        await SnapshotRouteAsync(nameof(Tasks_Drawer_Edit_With_Thread), "/tasks?drawer=t_1", "tasks-drawer-edit.png");
    }

    [Fact]
    public async Task Tasks_Drawer_Skeleton()
    {
        await SnapshotRouteAsync(nameof(Tasks_Drawer_Skeleton), "/tasks?drawer=t_1&drawerSkeleton=1&threadSkeleton=1", "tasks-drawer-skeleton.png");
    }

    [Fact]
    public async Task Tasks_Create_Shows_Validation_Then_Succeeds()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Tasks_Create_Shows_Validation_Then_Succeeds));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync($"{fixture.BaseUrl}/tasks?drawer=new&status=todo&page=1&pageSize=10");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.GetByTestId("create-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("create-title-error")).ToBeVisibleAsync();

            await page.GetByLabel("Title").FillAsync("Follow up: Contoso pricing review");
            await page.GetByTestId("create-save").ClickAsync();

            await Assertions.Expect(page.GetByTestId("toast")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByLabel("Create task pane")).ToBeHiddenAsync();

            await Assertions.Expect(page.GetByRole(AriaRole.Table).GetByText("Follow up: Contoso pricing review")).ToBeVisibleAsync();
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Tasks_Create_Shows_Validation_Then_Succeeds), "tasks-create.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tasks_Create_Shows_Nested_Validation_Errors()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Tasks_Create_Shows_Nested_Validation_Errors));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync($"{fixture.BaseUrl}/tasks?drawer=new&status=todo&page=1&pageSize=10");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Trigger multiple FluentValidation rules at once (including RuleForEach on arrays).
            // - Title: required
            // - CollaboratorIds: contains empty item -> CollaboratorIds[1]
            // - Tags: contains empty item -> Tags[1]
            // - EstimateMinutes: negative
            await page.GetByLabel("Collaborator IDs (comma-separated)").FillAsync("u_1,,u_2");
            await page.GetByLabel("Tags (comma-separated)").FillAsync("alpha,,beta");
            await page.GetByLabel("Estimate minutes").FillAsync("-5");

            await page.GetByTestId("create-save").ClickAsync();

            await Assertions.Expect(page.GetByTestId("create-error-summary")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("create-error-summary")).ToContainTextAsync("CollaboratorIds[1]");
            await Assertions.Expect(page.GetByTestId("create-error-summary")).ToContainTextAsync("Tags[1]");
            await Assertions.Expect(page.GetByTestId("create-error-summary")).ToContainTextAsync("EstimateMinutes");
            await Assertions.Expect(page.GetByTestId("create-title-error")).ToBeVisibleAsync();

            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Tasks_Create_Shows_Nested_Validation_Errors), "tasks-create-nested-validation.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Board_Page()
    {
        await SnapshotRouteAsync(nameof(Board_Page), "/tasks/board", "board.png");
    }

    [Fact]
    public async Task Board_Drawer_Edit()
    {
        await SnapshotRouteAsync(nameof(Board_Drawer_Edit), "/tasks/board?drawer=t_2", "board-drawer-edit.png");
    }

    [Fact]
    public async Task Accounts_Page()
    {
        await SnapshotRouteAsync(nameof(Accounts_Page), "/accounts", "accounts.png");
    }

    [Fact]
    public async Task Account_Detail_Page()
    {
        await SnapshotRouteAsync(nameof(Account_Detail_Page), "/accounts/a_2", "account-detail.png");
    }

    [Fact]
    public async Task Settings_Page()
    {
        await SnapshotRouteAsync(nameof(Settings_Page), "/settings", "settings.png");
    }

    [Fact]
    public async Task Notifications_Panel_Open()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Notifications_Panel_Open));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByTestId("top-notifications").ClickAsync();
            await Assertions.Expect(page.GetByLabel("Notifications panel")).ToBeVisibleAsync();
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Notifications_Panel_Open), "notifications.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Modal_Open()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Modal_Open));
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/tasks?drawer=t_1");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByTestId("taskdrawer-about-modes").ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Modal_Open), "modal.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Toast_Visible()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Toast_Visible));
        try
        {
            await page.GotoAsync($"{fixture.BaseUrl}/tasks?drawer=t_1");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByRole(AriaRole.Button, new() { Name = "Thread" }).ClickAsync();
            await page.GetByPlaceholder("Ask the agent, add context, or send an update…").FillAsync("Looks good — shipping this now.");
            await page.GetByRole(AriaRole.Button, new() { Name = "Send as Human" }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("toast")).ToBeVisibleAsync();
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Toast_Visible), "toast.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Page_Skeleton()
    {
        await SnapshotRouteAsync(nameof(Page_Skeleton), "/tasks?skeleton=1", "page-skeleton.png");
    }

    [Fact]
    public async Task Dashboard_Sse_Started()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Sse_Started));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByTestId("sse-start").ClickAsync();
            await Assertions.Expect(page.GetByTestId("sse-status")).ToContainTextAsync("open");
            await Assertions.Expect(page.GetByTestId("sse-last")).ToContainTextAsync("Stream tick");
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Dashboard_Sse_Started), "dashboard-sse.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dashboard_Streamed_Completed()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Streamed_Completed));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByTestId("streamed-start").ClickAsync();
            await Assertions.Expect(page.GetByTestId("streamed-status")).ToContainTextAsync("done", new() { Timeout = 15000 });
            await Assertions.Expect(page.GetByTestId("streamed-last")).ToContainTextAsync("#");
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Dashboard_Streamed_Completed), "dashboard-streamed.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dashboard_Lazy_Loaded()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Lazy_Loaded));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByRole(AriaRole.Button, new() { Name = "Load forecast" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Forecast is stable.")).ToBeVisibleAsync();
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Dashboard_Lazy_Loaded), "dashboard-lazy.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Dashboard_Mutation_Invalidates_Deferred()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Mutation_Invalidates_Deferred));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var before = await page.GetByTestId("insights-summary").InnerTextAsync();
            await page.GetByTestId("dashboard-create-task").ClickAsync();
            await Assertions.Expect(page.GetByTestId("insights-summary")).Not.ToHaveTextAsync(before);
            var after = await page.GetByTestId("insights-summary").InnerTextAsync();

            Assert.NotEqual(before, after);
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Dashboard_Mutation_Invalidates_Deferred), "dashboard-mutation.png");
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }

    private async Task SnapshotRouteAsync(string testName, string path, string snapshotFile)
    {
        var (context, page, diag) = await fixture.NewPageAsync(testName);
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync($"{fixture.BaseUrl}{path}");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await SnapshotAssertions.AssertMatchesAsync(page, diag, testName, snapshotFile);
        }
        catch (Exception ex)
        {
            await diag.CaptureFailureAsync(page, ex);
            throw;
        }
        finally
        {
            await diag.FlushAsync();
            await context.DisposeAsync();
        }
    }
}

