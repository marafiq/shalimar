using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class NavigationTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task Home_Renders()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Home_Renders));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Welcome");
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
