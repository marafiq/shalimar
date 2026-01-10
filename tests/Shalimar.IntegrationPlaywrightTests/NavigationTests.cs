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
        var page = await fixture.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl);
        await Assertions.Expect(page.Locator("h1")).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task No_Console_Errors()
    {
        var errors = new List<string>();
        var page = await fixture.NewPageAsync();
        page.Console += (_, msg) => { if (msg.Type == "error") errors.Add(msg.Text); };

        await page.GotoAsync(fixture.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Empty(errors);
    }
}
