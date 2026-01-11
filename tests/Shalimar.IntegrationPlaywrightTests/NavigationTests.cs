using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class NavigationTests(SandboxAppFixture fixture)
{
    [Fact]
    public async Task Home_Renders()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Home_Renders));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Shalimar" }))
                .ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("env-pill")).ToContainTextAsync("Env:");
            await SnapshotAssertions.AssertMatchesAsync(page, diag, nameof(Home_Renders), "dashboard.png");
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
    public async Task No_Console_Errors()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(No_Console_Errors));
        var errors = new List<string>();
        page.Console += (_, msg) => { if (msg.Type == "error") errors.Add(msg.Text); };

        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Empty(errors);
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
