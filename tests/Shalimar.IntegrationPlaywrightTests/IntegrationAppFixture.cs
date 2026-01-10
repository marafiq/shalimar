using Microsoft.Playwright;
using Xunit;

namespace Shalimar.IntegrationPlaywrightTests;

public class IntegrationAppFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl => Environment.GetEnvironmentVariable("INTEGRATION_APP_URL")
        ?? "http://localhost:5099";

    public string ArtifactsRoot { get; } = GetArtifactsRoot();

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task<(IBrowserContext Context, IPage Page, PlaywrightDiagnostics Diagnostics)> NewPageAsync(string testName)
    {
        Directory.CreateDirectory(ArtifactsRoot);

        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl
        });

        var page = await context.NewPageAsync();
        var diagnostics = new PlaywrightDiagnostics(ArtifactsRoot, testName);
        diagnostics.Attach(page);

        return (context, page, diagnostics);
    }

    public async Task<string> GetHtmlAsync(string path = "/")
    {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        return await client.GetStringAsync(path);
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }

    private static string GetArtifactsRoot()
    {
        var env = Environment.GetEnvironmentVariable("SHALIMAR_TEST_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        // Keep this under tests/ so `dotnet test` artifacts are easy to locate and can be ignored by git.
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestResults", "playwright"));
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationAppFixture> { }

public sealed class PlaywrightDiagnostics
{
    private readonly string _root;
    private readonly string _testName;
    private readonly List<string> _console = new();
    private readonly List<string> _pageErrors = new();

    public PlaywrightDiagnostics(string root, string testName)
    {
        _root = root;
        _testName = Sanitize(testName);
    }

    public void Attach(IPage page)
    {
        page.Console += (_, msg) =>
        {
            var line = $"[{DateTimeOffset.UtcNow:O}] console.{msg.Type}: {msg.Text}";
            _console.Add(line);
        };

        page.PageError += (_, err) =>
        {
            var line = $"[{DateTimeOffset.UtcNow:O}] pageerror: {err}";
            _pageErrors.Add(line);
        };
    }

    public async Task FlushAsync()
    {
        var testDir = Path.Combine(_root, _testName);
        Directory.CreateDirectory(testDir);

        await File.WriteAllLinesAsync(Path.Combine(testDir, "console.log"), _console);
        await File.WriteAllLinesAsync(Path.Combine(testDir, "page-errors.log"), _pageErrors);
    }

    public async Task CaptureFailureAsync(IPage page, Exception ex)
    {
        var testDir = Path.Combine(_root, _testName);
        Directory.CreateDirectory(testDir);

        await FlushAsync();

        await File.WriteAllTextAsync(Path.Combine(testDir, "exception.txt"), ex.ToString());

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(testDir, "failure.png"),
                FullPage = true
            });
        }
        catch
        {
            // Best effort; don't hide the real test failure.
        }

        try
        {
            var html = await page.ContentAsync();
            await File.WriteAllTextAsync(Path.Combine(testDir, "failure.html"), html);
        }
        catch
        {
            // Best effort; don't hide the real test failure.
        }
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
