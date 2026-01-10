using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ShellTests(IntegrationAppFixture fixture)
{
    [Fact]
    public async Task Shell_Contains_Context()
    {
        var html = await fixture.GetHtmlAsync();
        Assert.Contains("__SHALIMAR_CONTEXT__", html);
    }

    [Fact]
    public async Task Shell_Contains_Version()
    {
        var html = await fixture.GetHtmlAsync();
        Assert.Contains("__SHALIMAR_VERSION__", html);
    }

    [Fact]
    public async Task Shell_Contains_Environment()
    {
        var html = await fixture.GetHtmlAsync();
        Assert.Contains("\"environment\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Shell_Has_Hashed_Assets()
    {
        var html = await fixture.GetHtmlAsync();
        Assert.Contains("/dist/assets/", html);
    }
}
