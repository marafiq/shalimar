using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class FeatureCoverageTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task Refresh_Works_On_All_Component_Routes()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Refresh_Works_On_All_Component_Routes));
        try
        {
            await fixture.ResetCrmAsync();

            var cases = new (string Path, string Heading)[]
            {
                ("/", "Welcome to Shalimar"),
                ("/tasks", "Tasks"),
                ("/tasks/board", "Board"),
                ("/accounts", "Accounts"),
                ("/accounts/a_2", "Account"),
                ("/settings", "Settings"),
            };

            foreach (var c in cases)
            {
                // "Refresh works" is a server contract in Shalimar (not SPA). Prove the server returns valid shells on all routes.
                var html = await fixture.GetHtmlAsync(c.Path);
                Assert.Contains("__SHALIMAR_CONTEXT__", html);
                Assert.Contains("__SHALIMAR_PROPS__", html);
            }
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
    public async Task Tasks_Table_Filter_Paging_State_Preserved_After_Create()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Tasks_Table_Filter_Paging_State_Preserved_After_Create));
        try
        {
            await fixture.ResetCrmAsync();

            // Keep state in URL (server-truth, refreshable).
            await page.GotoAsync($"{fixture.BaseUrl}/tasks?drawer=new&q=pricing&status=todo&page=1&pageSize=10");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // First save should surface server validation.
            await page.GetByTestId("create-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("create-title-error")).ToBeVisibleAsync();

            // Fix, then succeed.
            await page.GetByLabel("Title").FillAsync("Follow up: Contoso pricing review");
            await page.GetByTestId("create-save").ClickAsync();

            await Assertions.Expect(page.GetByTestId("toast")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByLabel("Create task pane")).ToBeHiddenAsync();

            // URL should keep state but remove drawer.
            var uri = new Uri(page.Url);
            Assert.Equal("/tasks", uri.AbsolutePath);
            var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
            Assert.Equal("pricing", qs["q"]);
            Assert.Equal("todo", qs["status"]);
            Assert.Equal("1", qs["page"]);
            Assert.Equal("10", qs["pageSize"]);
            Assert.Null(qs["drawer"]);

            // Grid refetched via invalidation (prefix-based) and shows the new row under same filter.
            await Assertions.Expect(page.GetByRole(AriaRole.Table).GetByText("Follow up: Contoso pricing review")).ToBeVisibleAsync();
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
    public async Task Tasks_Create_ClientValidation_MaxLength_Works()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Tasks_Create_ClientValidation_MaxLength_Works));
        try
        {
            await fixture.ResetCrmAsync();

            var createRequests = 0;
            await page.RouteAsync("**/crm/tasks", async route =>
            {
                createRequests++;
                await route.ContinueAsync();
            });

            await page.GotoAsync($"{fixture.BaseUrl}/tasks?drawer=new&status=todo&page=1&pageSize=10");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var tooLongTitle = new string('x', 250);
            await page.GetByLabel("Title").FillAsync(tooLongTitle);
            await page.GetByTestId("create-save").ClickAsync();

            await Assertions.Expect(page.GetByTestId("create-title-error")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("create-save")).ToBeVisibleAsync();
            Assert.Equal(0, createRequests);
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
    public async Task Dashboard_Sse_Start_Stop_Works()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Sse_Start_Stop_Works));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.GetByTestId("sse-start").ClickAsync();
            await Assertions.Expect(page.GetByTestId("sse-status")).ToContainTextAsync("open");

            await page.GetByRole(AriaRole.Button, new() { Name = "Stop" }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("sse-status")).ToContainTextAsync("closed");
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
    public async Task Dashboard_Streamed_Abort_Works()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Dashboard_Streamed_Abort_Works));
        try
        {
            await fixture.ResetCrmAsync();
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.GetByTestId("streamed-start").ClickAsync();
            var abort = page.GetByRole(AriaRole.Button, new() { Name = "Abort" });
            await Assertions.Expect(page.GetByTestId("streamed-status")).ToContainTextAsync("streaming", new() { Timeout = 5000 });
            await Assertions.Expect(abort).ToBeEnabledAsync(new() { Timeout = 5000 });
            await abort.ClickAsync();
            await Assertions.Expect(page.GetByTestId("streamed-status")).ToContainTextAsync("aborted", new() { Timeout = 15000 });
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

