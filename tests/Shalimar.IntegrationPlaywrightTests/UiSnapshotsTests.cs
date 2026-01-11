using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class UiSnapshotsTests(IntegrationAppFixture fixture)
{
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
    public async Task Dashboard_Streamed_Started()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Streamed_Started));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByTestId("stream-start").ClickAsync();
            await Assertions.Expect(page.GetByTestId("stream-status")).ToContainTextAsync("open");
            await Assertions.Expect(page.GetByTestId("stream-last")).ToContainTextAsync("Stream tick");
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Dashboard_Streamed_Started), "dashboard-stream.png");
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

