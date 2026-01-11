using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public class ShellTests(SandboxAppFixture fixture)
{
    [Fact]
    public async Task Shell_Contains_Context()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Shell_Contains_Context));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            var html = await fixture.GetHtmlAsync();
            Assert.Contains("__SHALIMAR_CONTEXT__", html);
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
    public async Task Shell_Contains_Version()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Shell_Contains_Version));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            var html = await fixture.GetHtmlAsync();
            Assert.Contains("__SHALIMAR_VERSION__", html);
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
    public async Task Shell_Contains_Environment()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Shell_Contains_Environment));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            var html = await fixture.GetHtmlAsync();
            Assert.Contains("\"environment\"", html, StringComparison.OrdinalIgnoreCase);
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
    public async Task Shell_Has_Hashed_Assets()
    {
        var (context, page, diag) = await fixture.NewPageAsync(nameof(Shell_Has_Hashed_Assets));
        try
        {
            await page.GotoAsync(fixture.BaseUrl);
            var html = await fixture.GetHtmlAsync();
            Assert.Contains("/dist/assets/", html);
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
